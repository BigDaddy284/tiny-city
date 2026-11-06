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
    /// Stress runs of Tiny City 2's open land without Unity, against a slow, unreliable stand-in for each Ollama model,
    /// at each speed. Writes a Markdown report.
    /// </summary>
    public static partial class Program
    {
        private const double FrameSeconds = 1.0 / 60;
        private const int AutosaveEveryGameMinutes = 6 * 60; // as GameController
        private const int MaxTicksPerFrame = 600;

        private static readonly StringBuilder Report = new StringBuilder();

        /// <summary>Requests the game sends at once, and the stand-in Ollama's parallel slots to match.</summary>
        private static int ParallelRequests = OllamaSettings.DefaultParallelRequests;

        private static OllamaSettings Settings() => new OllamaSettings { parallelRequests = ParallelRequests };

        public static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var path = args.Length > 0 ? args[0] : "stress-report.md";
            var v2Days = args.Length > 1 ? double.Parse(args[1], CultureInfo.InvariantCulture) : 1;
            ParallelRequests = args.Length > 2 ? int.Parse(args[2]) : OllamaSettings.DefaultParallelRequests;

            Line("# Tiny City 2 stress test");
            Line();
            Line($"Run with .NET {Environment.Version} on {Environment.ProcessorCount} cores ({RuntimeInfo()}), {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC.");
            Line("Unity's Mono runs the same simulation code roughly 2–3 times slower.");
            Line();

            var ok = true;
            var section = 1;
            foreach (var profile in AiProfile.All)
            {
                // The CPU-only model is so slow that half a day is plenty to see the pattern.
                var d = profile.MinSeconds >= 10 ? Math.Min(v2Days, 0.5) : v2Days;
                ok &= v2Days <= 0 || AiRunsEverything(section++, profile, d);
            }

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

        // ---- A slow, unreliable stand-in for Ollama ----

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
        /// A stand-in for one Ollama model with <see cref="Slots"/> parallel slots (OLLAMA_NUM_PARALLEL): it works on up
        /// to that many requests at once, starting them in the order they arrive, and drops a request the moment its
        /// caller gives up on it. Each reply takes the profile's time, stretched by <see cref="ParallelSlowdown"/> for
        /// every other request running beside it; 1 in 30 is not JSON and 1 in 50 is lost (the caller times out).
        /// </summary>
        private sealed class SlowOllama
        {
            private readonly AiProfile profile;
            private readonly Queue<Job> waiting = new Queue<Job>();
            private readonly List<Job> running = new List<Job>();
            private readonly List<double> freeSlots = new List<double>();
            private int number;

            private sealed class Job
            {
                public FakeAiCall Call;
                public double Arrived;
                public double Seconds;
                public string Reply;
                public bool Lost;
                public double Start;
                public double Finish;
            }

            public SlowOllama(AiProfile profile, int slots = 1)
            {
                this.profile = profile;
                Slots = Math.Max(1, slots);
                for (var i = 0; i < Slots; i++)
                {
                    freeSlots.Add(0);
                }

                Transport = new FakeAiTransport { Responder = Answer };
            }

            /// <summary>
            /// How much longer each reply takes for every other request being answered beside it. An assumption, not a
            /// measurement: a small model on a graphics card is limited by memory bandwidth, so two at once take about
            /// 1.3 times as long each (about 1.5 times the throughput), and four at once about 1.9 times.
            /// </summary>
            public const double ParallelSlowdown = 0.3;

            public int Slots { get; }

            public FakeAiTransport Transport { get; }

            public double Now { get; set; }

            public bool Down { get; set; }

            public int Calls { get; private set; }

            /// <summary>
            /// A decision answer that plans two more steps is about 40 tokens longer, so it is taken to need this much
            /// more time. An assumption to check against a real model.
            /// </summary>
            public const double PlanReplyExtra = 0.25;

            /// <summary>Slot-seconds spent working on requests, finished or not, divided by the number of slots.</summary>
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
                var extra = 1.0;
                var kind = KindOf(call.Body);
                CallsByKind[kind] = (CallsByKind.TryGetValue(kind, out var k) ? k : 0) + 1;
                if (number % 30 == 5)
                {
                    content = "Hmm, let me think about that.";
                }
                else if (kind == "day plan")
                {
                    content = "{\"plan\":[{\"hour\":8,\"plan\":\"Breakfast at home\"},{\"hour\":9,\"plan\":\"Work\"},{\"hour\":13,\"plan\":\"Lunch\"},{\"hour\":18,\"plan\":\"See friends\"},{\"hour\":22,\"plan\":\"Bed\"}]}";
                    extra += PlanReplyExtra;
                }
                else if (kind == "react")
                {
                    content = "{\"react\":" + (number % 3 == 0 ? "true" : "false") + ",\"reason\":\"Why not\"}";
                }
                else if (kind == "insight")
                {
                    content = "{\"insights\":[{\"insight\":\"The city keeps me busy.\",\"because\":[1,2]}]}";
                }
                else if (kind == "rating")
                {
                    var ids = Regex.Matches(call.Body, "mem-\\d{6}").Cast<Match>().Select(m => m.Value).Distinct();
                    content = "{\"ratings\":[" + string.Join(",", ids.Select(id => "{\"id\":\"" + id + "\",\"importance\":3}")) + "]}";
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
                    extra += PlanReplyExtra;
                }

                var spread = profile.MaxSeconds - profile.MinSeconds;
                var latency = (profile.MinSeconds + spread * (((number * 7919) % 101) / 100.0)) * extra;
                waiting.Enqueue(new Job { Call = call, Arrived = Now, Seconds = latency, Reply = FakeAiTransport.ChatReply(content), Lost = number % 50 == 17 });
                return null;
            }

            /// <summary>Requests answered, by what they were for.</summary>
            public Dictionary<string, int> CallsByKind { get; } = new Dictionary<string, int>();

            private static string KindOf(string body) =>
                body.Contains("Plan the rest of your day") ? "day plan"
                : body.Contains("go over to talk") ? "react"
                : body.Contains("What do they tell you") ? "insight"
                : body.Contains("Rate how much each one matters") ? "rating"
                : body.Contains("You are chatting with") ? "line"
                : body.Contains("diary-style memory") ? "reflection"
                : "decision";

            /// <summary>Picks one of the options the prompt offers, the way a well-behaved model would.</summary>
            private string PickFreeTimeOption(string body)
            {
                var options = Regex.Matches(body.Replace("\\\"", "\""), "- action \"([^\"]+)\", target_id \"([^\"]*)\"");
                if (options.Count == 0)
                {
                    return "{\"action\":\"relax\",\"target_id\":\"none\",\"reason\":\"Resting\",\"say\":\"Ahh.\"}";
                }

                string Step(Match m) => "{\"action\":\"" + m.Groups[1].Value + "\",\"target_id\":\"" + m.Groups[2].Value + "\"}";
                var pick = options[number % options.Count];
                var then = Step(options[(number + 3) % options.Count]) + "," + Step(options[(number + 7) % options.Count]);
                return "{\"action\":\"" + pick.Groups[1].Value + "\",\"target_id\":\"" + pick.Groups[2].Value + "\",\"reason\":\"Feels right\",\"say\":\"Right then.\",\"then\":[" + then + "]}";
            }

            /// <summary>Works through the requests up to <see cref="Now"/>.</summary>
            public void Deliver()
            {
                var changed = true;
                while (changed)
                {
                    changed = false;
                    foreach (var job in running.ToList())
                    {
                        if (job.Call.IsDone)
                        {
                            // The caller gave up (cancelled): Ollama stops generating when the connection closes.
                            var stoppedAt = Math.Max(job.Start, Math.Min(job.Finish, Now));
                            BusySeconds -= (job.Finish - stoppedAt) / Slots;
                            running.Remove(job);
                            freeSlots.Add(stoppedAt);
                            changed = true;
                        }
                        else if (job.Finish <= Now)
                        {
                            if (!job.Lost)
                            {
                                job.Call.Succeed(job.Reply);
                                Served++;
                            }

                            running.Remove(job);
                            freeSlots.Add(job.Finish);
                            changed = true;
                        }
                    }

                    while (waiting.Count > 0 && running.Count < Slots)
                    {
                        var next = waiting.Dequeue();
                        if (next.Call.IsDone)
                        {
                            continue;
                        }

                        var slot = freeSlots.Min();
                        freeSlots.Remove(slot);
                        next.Start = Math.Max(slot, next.Arrived);
                        QueuedSeconds += next.Start - next.Arrived;
                        var seconds = next.Seconds * (1 + (ParallelSlowdown * running.Count));
                        next.Finish = next.Start + seconds;
                        BusySeconds += seconds / Slots;
                        running.Add(next);
                        changed = true;
                    }
                }

                for (var i = 0; i < freeSlots.Count; i++)
                {
                    freeSlots[i] = Math.Max(freeSlots[i], Now);
                }
            }
        }

        private static string Hours(double seconds) => seconds >= 3600 ? $"{seconds / 3600:0.0} h" : $"{seconds / 60:0} min";
    }
}
