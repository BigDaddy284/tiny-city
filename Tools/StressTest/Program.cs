using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TinyCity.Simulation;
using TinyCity.Simulation.AI;
using TinyCity.Simulation.Configuration;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Persistence;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;
using TinyCity.Tests.EditMode;

namespace TinyCity.Tools.StressTest
{
    /// <summary>
    /// Stress runs of the whole town without Unity: a long run with the built-in decision maker, the game loop at
    /// each speed against a slow, unreliable stand-in for Ollama, and an Ollama outage. Writes a Markdown report.
    /// </summary>
    public static partial class Program
    {
        private const double FrameSeconds = 1.0 / 60;
        private const int AutosaveEveryGameMinutes = 6 * 60; // as GameController
        private const int MaxTicksPerFrame = 600;

        private static readonly StringBuilder Report = new StringBuilder();

        public static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var path = args.Length > 0 ? args[0] : "stress-report.md";
            var days = args.Length > 1 ? int.Parse(args[1]) : 336;
            var aiDays = args.Length > 2 ? int.Parse(args[2]) : 3;
            var bigDays = args.Length > 3 ? int.Parse(args[3]) : 2;

            Line("# Tiny City stress test");
            Line();
            Line($"Run with .NET {Environment.Version} on {Environment.ProcessorCount} cores ({RuntimeInfo()}), {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC.");
            Line("Unity's Mono runs the same simulation code roughly 2–3 times slower (the soak test takes about 2 minutes under .NET and 5 in Unity).");
            Line();

            var ok = days == 0 || LongRun(days);
            var section = 2;
            foreach (var profile in AiProfile.All)
            {
                ok &= aiDays == 0 || AiAtEachSpeed($"## {section++}. Game loop with {profile.Name}", profile, aiDays, StarterTown.CreateNewGame);
            }

            ok &= Outage(section++);
            ok &= bigDays == 0 || BigCity(section, bigDays);

            File.WriteAllText(path, Report.ToString());
            Console.WriteLine(Report.ToString());
            Console.WriteLine(ok ? "All checks held." : "SOME CHECKS FAILED.");
            return ok ? 0 : 1;
        }

        private static string RuntimeInfo() => System.Runtime.InteropServices.RuntimeInformation.OSDescription.Trim();

        private static void Line(string text = "")
        {
            Report.AppendLine(text);
            Console.Error.WriteLine(text);
        }

        // ---- 1. Long run, built-in decisions only ----

        private static bool LongRun(int days)
        {
            Line($"## 1. {days} game days with the built-in decision maker");
            Line();
            Line("The town runs flat out, one tick per game minute, with an autosave every 6 game hours as in the game. Each row is 28 game days (one resident year).");
            Line();
            Line("| Days | Alive | ms per game day | Slowest tick (ms) | 99.9% of ticks under (ms) | Heap after GC (MB) | Save size (KB) | Save (ms) | Load (ms) | Memories | Chats | Ledger | History |");
            Line("|---|---|---|---|---|---|---|---|---|---|---|---|---|");

            var sim = StarterTown.CreateNewGame();
            var world = StarterTown.CreateWorld();
            var block = new List<double>();
            var blockMs = 0.0;
            var saveMs = new List<double>();
            var worstSaveMs = 0.0;
            var firstMsPerDay = 0.0;
            var lastMsPerDay = 0.0;
            var firstHeap = 0.0;
            var lastHeap = 0.0;
            var ok = true;
            var everything = Stopwatch.StartNew();

            for (var day = 1; day <= days; day++)
            {
                for (var minute = 0; minute < GameTime.MinutesPerDay; minute++)
                {
                    var t = Stopwatch.GetTimestamp();
                    sim.Tick();
                    var ms = Ms(Stopwatch.GetTimestamp() - t);
                    block.Add(ms);
                    blockMs += ms;
                    if (sim.Time.TotalMinutes % AutosaveEveryGameMinutes == 0)
                    {
                        t = Stopwatch.GetTimestamp();
                        SaveSerializer.Save(sim);
                        var s = Ms(Stopwatch.GetTimestamp() - t);
                        saveMs.Add(s);
                        worstSaveMs = Math.Max(worstSaveMs, s);
                    }
                }

                if (day % 28 == 0 || day == days)
                {
                    var t = Stopwatch.GetTimestamp();
                    var json = SaveSerializer.Save(sim);
                    var save = Ms(Stopwatch.GetTimestamp() - t);
                    t = Stopwatch.GetTimestamp();
                    var loaded = SaveSerializer.Load(json, world);
                    var load = Ms(Stopwatch.GetTimestamp() - t);
                    if (!loaded.Success || loaded.Warnings.Count > 0 || SaveSerializer.Save(loaded.Simulation) != json)
                    {
                        Line($"**Day {day}: the save did not load back exactly.** {string.Join("; ", loaded.Errors.Concat(loaded.Warnings))}");
                        ok = false;
                    }

                    var heap = GC.GetTotalMemory(true) / 1048576.0;
                    var daysInBlock = (day - 1) % 28 + 1;
                    var msPerDay = blockMs / daysInBlock;
                    block.Sort();
                    Line($"| {day} | {sim.Residents.Count(r => r.IsAlive)} | {msPerDay:0.0} | {block[block.Count - 1]:0.00} | {block[(int)(block.Count * 0.999)]:0.000} | {heap:0.0} | {json.Length / 1024.0:0} | {save:0.0} | {load:0.0} | {sim.Memories.All.Count} | {sim.Conversations.All.Count} | {sim.Economy.Ledger.All.Count} | {sim.History.Events.Count} |");
                    if (day == 28 || day == days && days < 28)
                    {
                        firstMsPerDay = msPerDay;
                    }

                    if (day == 168 || day == days && days < 168)
                    {
                        // By now every capped record (chats, ledger) is full, so the heap should stop growing.
                        firstHeap = heap;
                    }

                    lastMsPerDay = msPerDay;
                    lastHeap = heap;
                    block.Clear();
                    blockMs = 0;
                }
            }

            saveMs.Sort();
            Line();
            Line($"Whole run: {everything.Elapsed.TotalSeconds:0.0} s for {days} game days ({days * GameTime.MinutesPerDay / everything.Elapsed.TotalSeconds:0} ticks a second, with saves and checks). Autosaves: median {saveMs[saveMs.Count / 2]:0.0} ms, slowest {worstSaveMs:0.0} ms.");
            var slowdown = firstMsPerDay > 0 ? lastMsPerDay / firstMsPerDay : 1;
            var growth = lastHeap - firstHeap;
            Line($"The last block took {slowdown:0.00}× as long per day as the first. From day {Math.Min(168, days)}, when the capped records are full, to the end the heap changed by {growth:+0.0;-0.0} MB.");
            if (slowdown > 1.5)
            {
                Line("**Ticks got noticeably slower as the town aged.**");
                ok = false;
            }

            if (growth > 5)
            {
                Line("**Memory kept growing.**");
                ok = false;
            }

            Line();
            return ok;
        }

        private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

        // ---- 2. The game loop with a slow, unreliable AI ----

        private sealed class AiProfile
        {
            public string Name;
            public string Description;
            public double MinSeconds;
            public double MaxSeconds;

            public static readonly AiProfile[] All =
            {
                new AiProfile { Name = "qwen2.5:7b on a graphics card", Description = "1.5 to 4 s a reply, like Qwen2.5 7B on a mid-range GPU", MinSeconds = 1.5, MaxSeconds = 4 },
                new AiProfile { Name = "qwen2.5:7b on the CPU only", Description = "12 to 30 s a reply, like Qwen2.5 7B without a usable GPU", MinSeconds = 12, MaxSeconds = 30 },
                new AiProfile { Name = "qwen2.5:0.5b", Description = "0.3 to 0.5 s a reply (0.4 s on average, as measured by the owner with qwen2.5:0.5b at 10x)", MinSeconds = 0.3, MaxSeconds = 0.5 },
            };
        }

        /// <summary>
        /// A stand-in for one Ollama model: it works on one request at a time, in the order they arrive (as Ollama
        /// does with one model loaded and no parallel slots), and drops a request the moment its caller gives up on it.
        /// Each reply takes the profile's time; 1 in 30 is not JSON and 1 in 50 is lost (the caller times out).
        /// </summary>
        private sealed class SlowOllama
        {
            private readonly AiProfile profile;
            private readonly Queue<Job> waiting = new Queue<Job>();
            private Job current;
            private double freeAt;
            private int number;

            private sealed class Job
            {
                public FakeAiCall Call;
                public double Arrived;
                public double Seconds;
                public string Reply;
                public bool Lost;
                public double Finish;
            }

            public SlowOllama(AiProfile profile)
            {
                this.profile = profile;
                Transport = new FakeAiTransport { Responder = Answer };
            }

            public FakeAiTransport Transport { get; }

            public double Now { get; set; }

            public bool Down { get; set; }

            public int Calls { get; private set; }

            /// <summary>Seconds spent working on requests, finished or not.</summary>
            public double BusySeconds { get; private set; }

            /// <summary>Total seconds requests spent waiting for the model to be free.</summary>
            public double QueuedSeconds { get; private set; }

            public int Served { get; private set; }

            private FakeAiCall Answer(FakeAiCall call)
            {
                if (Down)
                {
                    return call.Fail("Cannot connect to destination host");
                }

                if (call.Url.EndsWith("/api/tags"))
                {
                    return call.Succeed("{\"models\":[{\"name\":\"" + FakeAiTransport.Model + "\"}]}");
                }

                if (call.Body.Contains("\"messages\": []"))
                {
                    return call.Succeed("{\"model\":\"" + FakeAiTransport.Model + "\",\"done\":true}");
                }

                number++;
                Calls++;
                string content;
                if (number % 30 == 5)
                {
                    content = "Hmm, let me think about that.";
                }
                else if (call.Body.Contains("You are chatting with"))
                {
                    content = "{\"say\":\"Busy day out there.\",\"tone\":\"" + (number % 3 == 0 ? "playful" : "warm") + "\",\"topic\":\"the city\",\"end\":false}";
                }
                else if (call.Body.Contains("diary-style memory"))
                {
                    content = "{\"summary\":\"A long, ordinary day in the city.\",\"importance\":2}";
                }
                else
                {
                    content = PickFreeTimeOption(call.Body);
                }

                var spread = profile.MaxSeconds - profile.MinSeconds;
                var latency = profile.MinSeconds + spread * (((number * 7919) % 101) / 100.0);
                waiting.Enqueue(new Job { Call = call, Arrived = Now, Seconds = latency, Reply = FakeAiTransport.ChatReply(content), Lost = number % 50 == 17 });
                return null;
            }

            /// <summary>Picks one of the options the prompt offers, the way a well-behaved model would.</summary>
            private string PickFreeTimeOption(string body)
            {
                var options = Regex.Matches(body.Replace("\\\"", "\""), "- action \"([^\"]+)\", target_id \"([^\"]*)\"");
                if (options.Count == 0)
                {
                    return "{\"action\":\"relax\",\"target_id\":\"none\",\"reason\":\"Resting\",\"say\":\"Ahh.\"}";
                }

                var pick = options[number % options.Count];
                return "{\"action\":\"" + pick.Groups[1].Value + "\",\"target_id\":\"" + pick.Groups[2].Value + "\",\"reason\":\"Feels right\",\"say\":\"Right then.\"}";
            }

            /// <summary>Works through the requests up to <see cref="Now"/>.</summary>
            public void Deliver()
            {
                while (true)
                {
                    if (current != null)
                    {
                        if (current.Call.IsDone)
                        {
                            // The caller gave up (cancelled): Ollama stops generating when the connection closes.
                            BusySeconds -= Math.Max(0, current.Finish - Math.Max(Now, current.Finish - current.Seconds));
                            freeAt = Math.Min(current.Finish, Now);
                            current = null;
                            continue;
                        }

                        if (current.Finish > Now)
                        {
                            return;
                        }

                        if (!current.Lost)
                        {
                            current.Call.Succeed(current.Reply);
                            Served++;
                        }

                        freeAt = current.Finish;
                        current = null;
                    }

                    if (waiting.Count == 0)
                    {
                        freeAt = Math.Max(freeAt, Now);
                        return;
                    }

                    var next = waiting.Dequeue();
                    if (next.Call.IsDone)
                    {
                        continue;
                    }

                    var start = Math.Max(freeAt, next.Arrived);
                    QueuedSeconds += start - next.Arrived;
                    next.Finish = start + next.Seconds;
                    BusySeconds += next.Seconds;
                    current = next;
                }
            }
        }

        private sealed class LoopResult
        {
            public int Decisions;
            public int AiDecisions;
            public int WaitingMinutes;
            public int WorstWait;
            public double WorstFrameMs;
            public double QueueSum;
            public int QueueSamples;
            public int LongestQueue;
            public int AiLines;
            public int Lines;
            public Dictionary<string, int> AiPerResident = new Dictionary<string, int>();
            public Dictionary<string, int> DecisionsPerResident = new Dictionary<string, int>();

            /// <summary>The lowest and highest share of a resident's own free-time choices the AI made, in percent.</summary>
            public (double low, double high) ShareRange()
            {
                var shares = DecisionsPerResident.Where(d => d.Value > 0).Select(d => 100.0 * (AiPerResident.TryGetValue(d.Key, out var n) ? n : 0) / d.Value).ToList();
                return shares.Count == 0 ? (0, 0) : (shares.Min(), shares.Max());
            }
            public AiQueueStats Stats;
            public bool Connected;
            public double RealSeconds;
            public double BusySeconds;
            public double TickMs;
            public int Ticks;
            public double SlowestTickMs;
            public double SlowestSaveMs;
            public int SaveBytes;
        }

        private static bool AiAtEachSpeed(string heading, AiProfile profile, int days, Func<TownSimulation> make, string about = null)
        {
            Line(heading);
            Line();
            if (about != null)
            {
                Line(about);
                Line();
            }

            Line($"The stand-in Ollama takes {profile.Description}, and works on one request at a time. One reply in 30 is not JSON and one in 50 never arrives (it times out). Frames run at 60 a second as in the game, with ticks, autosaves and AI updates where the game does them. Default settings: decisions wait up to {OllamaSettings.DefaultDecisionWaitMinutes} game minutes, requests time out after {OllamaSettings.DefaultTimeoutSeconds} s, {OllamaSettings.DefaultMaxRetries} retry. {days} game days at each speed.");
            Line();
            Line("| Speed | Real time | Free-time choices | Chosen by AI | AI lines in chats | Avg / longest queue | Avg reply (s) | Timed out | Not JSON | Stale | Dropped | Longest wait (game min) | AI share of each resident's choices (lowest / highest) | Model busy | Slowest frame (ms) |");
            Line("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");

            var ok = true;
            foreach (var speed in new[] { 1, 2, 4, 10 })
            {
                var result = RunLoop(profile, speed, days, make, null);
                var share = result.Decisions == 0 ? 0 : 100.0 * result.AiDecisions / result.Decisions;
                var lineShare = result.Lines == 0 ? 0 : 100.0 * result.AiLines / result.Lines;
                var s = result.Stats;
                Line($"| {speed}x | {Hours(result.RealSeconds)} | {result.Decisions} | {share:0}% | {lineShare:0}% | {result.QueueSum / Math.Max(1, result.QueueSamples):0.0} / {result.LongestQueue} | {s.AverageLatencySeconds:0.0} | {s.TimedOut} | {s.InvalidReplies} | {s.Stale} | {s.Dropped} | {result.WorstWait} | {result.ShareRange().low:0}% / {result.ShareRange().high:0}% | {100 * result.BusySeconds / result.RealSeconds:0}% | {result.WorstFrameMs:0.0} |");
                if (!result.Connected)
                {
                    Line($"**At {speed}x the AI went offline.**");
                    ok = false;
                }

                if (result.WorstWait > OllamaSettings.DefaultDecisionWaitMinutes + 1)
                {
                    Line($"**At {speed}x someone waited on the AI past the deadline.**");
                    ok = false;
                }

                if (result.LongestQueue > AiRequestQueue.DefaultMaxPending)
                {
                    Line($"**At {speed}x the queue grew to {result.LongestQueue}.**");
                    ok = false;
                }
            }

            Line();
            return ok;
        }

        private static string Hours(double seconds) => seconds >= 3600 ? $"{seconds / 3600:0.0} h" : $"{seconds / 60:0} min";

        private static LoopResult RunLoop(AiProfile profile, int speed, int days, Func<TownSimulation> make, Action<SlowOllama, AiService, LoopResult, double> each)
        {
            var sim = make();
            var ollama = new SlowOllama(profile);
            var ai = new AiService(new OllamaSettings(), ollama.Transport);
            ai.Attach(sim);
            var result = new LoopResult();
            var deciding = new Dictionary<string, int>();
            var waiting = new HashSet<string>();
            var seenLines = new HashSet<string>();
            var accumulator = 0.0;
            var lastAutosave = sim.Time.TotalMinutes;
            var end = sim.Time.TotalMinutes + (long)days * GameTime.MinutesPerDay;
            var now = 0.0;

            while (sim.Time.TotalMinutes < end)
            {
                var frameStart = Stopwatch.GetTimestamp();
                ollama.Now = now;
                ollama.Deliver();
                ai.Update(now);
                accumulator += FrameSeconds * speed;
                var ticks = 0;
                while (accumulator >= 1 && ticks < MaxTicksPerFrame)
                {
                    accumulator -= 1;
                    var tickStart = Stopwatch.GetTimestamp();
                    sim.Tick();
                    var tickMs = Ms(Stopwatch.GetTimestamp() - tickStart);
                    result.TickMs += tickMs;
                    result.Ticks++;
                    result.SlowestTickMs = Math.Max(result.SlowestTickMs, tickMs);
                    ticks++;
                    if (sim.Time.TotalMinutes - lastAutosave >= AutosaveEveryGameMinutes)
                    {
                        var saveStart = Stopwatch.GetTimestamp();
                        result.SaveBytes = SaveSerializer.Save(sim).Length;
                        result.SlowestSaveMs = Math.Max(result.SlowestSaveMs, Ms(Stopwatch.GetTimestamp() - saveStart));
                        lastAutosave = sim.Time.TotalMinutes;
                    }

                    foreach (var r in sim.Residents)
                    {
                        if (r.State == ResidentState.Deciding)
                        {
                            waiting.Add(r.Id);
                            deciding[r.Id] = deciding.TryGetValue(r.Id, out var m) ? m + 1 : 1;
                            result.WaitingMinutes++;
                            result.WorstWait = Math.Max(result.WorstWait, deciding[r.Id]);
                        }
                        else if (waiting.Remove(r.Id))
                        {
                            deciding[r.Id] = 0;
                            result.Decisions++;
                            result.DecisionsPerResident[r.Id] = result.DecisionsPerResident.TryGetValue(r.Id, out var d) ? d + 1 : 1;
                            if (r.GoalSource == DecisionSource.Ai)
                            {
                                result.AiDecisions++;
                                result.AiPerResident[r.Id] = result.AiPerResident.TryGetValue(r.Id, out var n) ? n + 1 : 1;
                            }
                        }
                    }
                }

                if (ticks > 0)
                {
                    foreach (var session in sim.ActiveConversations)
                    {
                        for (var i = 0; i < session.Lines.Count; i++)
                        {
                            if (seenLines.Add(session.Id + "/" + i))
                            {
                                result.Lines++;
                                if (session.Lines[i].FromAi)
                                {
                                    result.AiLines++;
                                }
                            }
                        }
                    }
                }

                result.WorstFrameMs = Math.Max(result.WorstFrameMs, Ms(Stopwatch.GetTimestamp() - frameStart));
                result.QueueSum += ai.Queue.PendingCount;
                result.QueueSamples++;
                result.LongestQueue = Math.Max(result.LongestQueue, ai.Queue.PendingCount);
                each?.Invoke(ollama, ai, result, now);
                now += FrameSeconds;
            }

            foreach (var r in sim.Residents.Where(r => r.IsAlive && !result.AiPerResident.ContainsKey(r.Id)))
            {
                result.AiPerResident[r.Id] = 0;
            }

            result.Stats = ai.Queue.Stats;
            result.BusySeconds = ollama.BusySeconds;
            result.Connected = ai.Status == AiConnectionStatus.Connected;
            result.RealSeconds = now;
            return result;
        }

        // ---- 3. Ollama goes away and comes back ----

        private static bool Outage(int section)
        {
            Line($"## {section}. Ollama stops for five minutes and comes back");
            Line();
            const int Speed = 4;
            const int Days = 2;
            const double DownAt = 120;
            const double UpAt = DownAt + 300;
            double? offlineAt = null;
            double? backAt = null;

            var result = RunLoop(AiProfile.All[0], Speed, Days, StarterTown.CreateNewGame, (ollama, ai, r, now) =>
            {
                ollama.Down = now >= DownAt && now < UpAt;
                if (now >= DownAt && offlineAt == null && ai.Status == AiConnectionStatus.Offline)
                {
                    offlineAt = now;
                }

                if (now >= UpAt && backAt == null && ai.Status == AiConnectionStatus.Connected)
                {
                    backAt = now;
                }
            });

            Line($"At {Speed}x, {Days} game days ({Days * GameTime.MinutesPerDay / Speed / 60} real minutes) with the graphics-card stand-in. Ollama stops answering after {DownAt / 60:0} real minutes and starts again {(UpAt - DownAt) / 60:0} minutes later.");
            Line();
            Line(offlineAt == null ? "- **The game never noticed that Ollama had gone.**" : $"- The game marked the AI offline {offlineAt - DownAt:0} s after Ollama stopped, and residents carried on with the built-in decision maker.");
            Line(backAt == null ? "- **The AI did not come back on its own.**" : $"- It reconnected {backAt - UpAt:0} s after Ollama came back (it checks every {AiService.RecheckSeconds:0} s).");
            Line($"- Over the run the AI made {result.AiDecisions} of {result.Decisions} free-time choices; the longest anyone waited on it was {result.WorstWait} game minutes.");
            Line();
            return offlineAt != null && backAt != null && result.WorstWait <= OllamaSettings.DefaultDecisionWaitMinutes + 1;
        }
    }
}
