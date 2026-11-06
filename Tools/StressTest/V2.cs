using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation;
using TinyCity.Simulation.AI;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;

namespace TinyCity.Tools.StressTest
{
    /// <summary>
    /// Tiny City 2's AI budget: the city runs only while nobody waits for the AI (as <c>GameController.HoldingForAi</c>),
    /// so this measures how many requests a game day takes, of which kinds, how long they queue, and how fast the
    /// city really runs at each speed.
    /// </summary>
    public static partial class Program
    {
        /// <summary>While the clock is held nothing moves, so the loop steps real time in bigger strides then.</summary>
        private const double HeldStepSeconds = 0.25;

        private static readonly string[] Kinds = { "decision", "day plan", "line", "react", "insight", "reflection", "rating" };

        private static bool AiRunsEverything(int section, AiProfile profile, double days)
        {
            Line($"## {section}. Tiny City 2 on the open land with {profile.Name}");
            Line();
            Line($"The AI makes every choice and the clock waits for it, as in the v2 game, on the flat map with its {FlatLand.MaxResidents} people (nobody has a bed, so they sleep on benches or the ground). The stand-in Ollama takes {profile.Description} and works on {ParallelRequests} request{(ParallelRequests == 1 ? string.Empty : "s")} at a time; one reply in 30 is not JSON and one in 50 never arrives. It picks options in turn rather than sensibly, so needs and deaths here say nothing about how a real model plays. {days:0.#} game day{(days == 1 ? string.Empty : "s")} at each speed.");
            Line();
            Line("| People | Speed | Real time | Actually ran at | Clock held | Requests per game day | " + string.Join(" | ", Kinds.Select(k => k + "s")) + " | Avg wait for a choice (s) | Longest (s) | Avg reply (s) | Gave up | Deaths |");
            Line("|---|---|---|---|---|---|" + string.Concat(Kinds.Select(_ => "---|")) + "---|---|---|---|---|");
            var ok = true;
            const int people = FlatLand.MaxResidents;
            foreach (var speed in new[] { 1, 2, 4 })
            {
                var sim = FlatLand.CreateNewGame();
                sim.AiRunsEverything = true;
                sim.FoodMustBeMade = true;
                var ollama = new SlowOllama(profile, ParallelRequests);
                var ai = new AiService(Settings(), ollama.Transport);
                ai.Attach(sim);
                ai.SetGameSpeed(speed);
                var start = sim.Time.TotalMinutes;
                var end = start + (long)(days * GameTime.MinutesPerDay);
                var now = 0.0;
                var accumulator = 0.0;
                var held = 0.0;
                var decidingSince = new Dictionary<string, double>();
                var waits = new List<double>();
                while (sim.Time.TotalMinutes < end && now < 7 * 24 * 3600)
                {
                    ollama.Now = now;
                    ollama.Deliver();
                    ai.Update(now);
                    var holding = ai.Status != AiConnectionStatus.Connected || sim.WaitingForAi;
                    var step = holding ? HeldStepSeconds : FrameSeconds;
                    accumulator += step * speed;
                    var ticks = 0;
                    while (accumulator >= 1 && ticks < MaxTicksPerFrame)
                    {
                        if (ai.Status != AiConnectionStatus.Connected || sim.WaitingForAi)
                        {
                            accumulator = Math.Min(accumulator, 1);
                            break;
                        }

                        accumulator -= 1;
                        sim.Tick();
                        ticks++;
                    }

                    if (holding)
                    {
                        held += step;
                    }

                    foreach (var r in sim.Residents)
                    {
                        if (r.State == ResidentState.Deciding)
                        {
                            if (!decidingSince.ContainsKey(r.Id))
                            {
                                decidingSince[r.Id] = now;
                            }
                        }
                        else if (decidingSince.TryGetValue(r.Id, out var since))
                        {
                            waits.Add(now - since);
                            decidingSince.Remove(r.Id);
                        }
                    }

                    now += step;
                }

                var gameDays = (sim.Time.TotalMinutes - start) / (double)GameTime.MinutesPerDay;
                var calls = ollama.CallsByKind.Values.Sum();
                var perDay = gameDays <= 0 ? 0 : calls / gameDays;
                var ran = now <= 0 ? 0 : (sim.Time.TotalMinutes - start) / now;
                var kindCells = string.Join(" | ", Kinds.Select(k => (ollama.CallsByKind.TryGetValue(k, out var n) ? n / Math.Max(gameDays, 1e-9) : 0).ToString("0")));
                var deaths = sim.Residents.Count(r => !r.IsAlive);
                var stats = ai.Queue.Stats;
                Line($"| {sim.LivingCount} | {speed}x | {Hours(now)} | {ran:0.00}x | {100 * held / Math.Max(now, 1e-9):0}% | {perDay:0} | {kindCells} | {(waits.Count == 0 ? 0 : waits.Average()):0.0} | {(waits.Count == 0 ? 0 : waits.Max()):0} | {stats.AverageLatencySeconds:0.0} | {stats.GaveUp} | {deaths} |");
                if (gameDays < days * 0.99)
                {
                    Line($"**With {people} people at {speed}x the land only got through {gameDays:0.00} of {days:0.#} game days in a simulated week of real time.**");
                    ok = false;
                }
            }

            Line();
            Line("\"Requests per game day\" and the columns by kind count every request sent to the model, retries included. \"Wait for a choice\" is real time from a resident starting to decide until they act, which is what holds the clock. \"Actually ran at\" is game minutes per real second, so 1.00x means the city never had to wait.");
            Line();
            return ok;
        }
    }
}
