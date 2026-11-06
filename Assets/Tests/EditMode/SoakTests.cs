using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation;
using TinyCity.Simulation.AI;
using TinyCity.Simulation.Configuration;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.History;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Persistence;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Social;
using TinyCity.Simulation.Time;

namespace TinyCity.Tests.EditMode
{
    /// <summary>
    /// Long runs of the open land: no corrupted saves, no uncontrolled queues or collections, and nobody stuck waiting on the AI.
    /// </summary>
    public class SoakTests
    {
        /// <summary>Four in-game weeks: a whole 28-day year on the open land.</summary>
        private const int SoakDays = 28;

        [Test]
        [Timeout(900000)]
        public void AYear_WithAStandInAi_SavedAndReloadedEveryWeek_MatchesAnUninterruptedRun()
        {
            var world = FlatLand.CreateWorld();
            var straight = FlatLand.CreateNewGame();
            var viaSaves = FlatLand.CreateNewGame();
            straight.AiDecisionsEnabled = true;
            viaSaves.AiDecisionsEnabled = true;
            var timer = Stopwatch.StartNew();
            long tickTicks = 0;

            for (var day = 1; day <= SoakDays; day++)
            {
                for (var minute = 0; minute < GameTime.MinutesPerDay; minute++)
                {
                    var before = Stopwatch.GetTimestamp();
                    TestTown.Answer(straight);
                    straight.Tick();
                    tickTicks += Stopwatch.GetTimestamp() - before;
                    TestTown.Answer(viaSaves);
                    viaSaves.Tick();
                }

                CheckInvariants(straight);
                if (day % 7 == 0)
                {
                    // Answered first, so nobody is saved halfway through a choice or a line (those are asked again after loading).
                    TestTown.Answer(straight);
                    TestTown.Answer(viaSaves);
                    var saved = SaveSerializer.Save(viaSaves);
                    var result = SaveSerializer.Load(saved, world);
                    Assert.That(result.Success, Is.True, $"day {day}: " + string.Join("; ", result.Errors));
                    Assert.That(result.Warnings, Is.Empty, $"day {day}: " + string.Join("; ", result.Warnings));
                    viaSaves = result.Simulation;
                    viaSaves.AiDecisionsEnabled = true;
                    Assert.That(SaveSerializer.Save(viaSaves), Is.EqualTo(saved), $"day {day}: a save loads back exactly");
                    Assert.That(saved, Is.EqualTo(SaveSerializer.Save(straight)), $"day {day}: saving and loading changed the town's future");
                }
            }

            var msPerDay = tickTicks * 1000.0 / Stopwatch.Frequency / SoakDays;
            TestContext.WriteLine($"Simulated {SoakDays} days in {timer.Elapsed.TotalSeconds:0.0} s (both runs, with saves); one run's ticks and choices average {msPerDay:0.0} ms per game day.");
            TestContext.WriteLine($"Memories {straight.Memories.All.Count}, chats {straight.Conversations.All.Count}, history {straight.History.Events.Count}.");
            Assert.That(straight.Residents.All(r => r.IsAlive), Is.True, "nobody dies on the open land");
        }

        /// <summary>Things that must hold at any moment, however long the town runs.</summary>
        private static void CheckInvariants(TownSimulation sim)
        {
            var label = $"day {sim.Time.Day}";
            Assert.That(sim.Residents.All(r => r.Memories(sim).Count() <= MemoryStore.MaxPerResident), Is.True, $"{label}: memories are capped");
            Assert.That(sim.Residents.All(r => r.Log.Count <= Resident.MaxLogEntries), Is.True, $"{label}: logs are capped");
            Assert.That(sim.Conversations.All.Count, Is.LessThanOrEqualTo(ConversationHistory.MaxRecords), label);
            Assert.That(sim.History.Events.Count, Is.LessThanOrEqualTo(TownHistory.MaxEvents), label);
            Assert.That(sim.ActiveConversations.Count, Is.LessThanOrEqualTo(sim.Residents.Count / 2), label);
            Assert.That(sim.Relationships.All.Count, Is.LessThanOrEqualTo(sim.Residents.Count * (sim.Residents.Count - 1)), label);

            foreach (var r in sim.Residents)
            {
                for (var i = 0; i < Needs.Count; i++)
                {
                    var value = r.Needs[(NeedType)i];
                    Assert.That(value >= Needs.Min && value <= Needs.Max, Is.True, $"{label}: {r.FirstName}'s {(NeedType)i} is {value}");
                }

                Assert.That(r.Health >= 0 && r.Health <= 100, Is.True, $"{label}: {r.FirstName}'s health is {r.Health}");
                if (!r.IsAlive)
                {
                    Assert.That(r.State, Is.EqualTo(ResidentState.Idle), $"{label}: {r.FirstName} has died and does nothing");
                    Assert.That(r.ConversationId, Is.Null, label);
                }
            }
        }

        // ---- AI under load ----

        private const string DialogueMarker = "You are chatting with";

        /// <summary>
        /// The longest anyone may wait on the AI here. Twenty people share one slow model that loses or garbles a
        /// quarter of its answers, so a choice can wait hours behind everyone else's (about ten at the last count),
        /// but an answer that never comes is asked for again, so nobody waits a whole day.
        /// </summary>
        private const int MaxMinutesDeciding = 24 * 60;
        private const string ReflectionMarker = "diary-style memory";
        private const string Relax = "{\"action\":\"relax\",\"target_id\":\"none\",\"reason\":\"Just resting\",\"say\":\"Ahh.\"}";

        [Test]
        public void TwoWeeks_WithASlowUnreliableAi_KeepsTheQueueBounded_AndNobodyStuck()
        {
            var sim = FlatLand.CreateNewGame();
            var now = 0.0;
            var due = new List<(FakeAiCall call, double at, string reply)>();
            var number = 0;
            var transport = new FakeAiTransport();
            transport.Responder = call =>
            {
                if (call.Url.EndsWith("/api/tags"))
                {
                    return call.Succeed("{\"models\":[{\"name\":\"" + FakeAiTransport.Model + "\"}]}");
                }

                if (call.Body.Contains("\"messages\": []"))
                {
                    return call.Succeed("{\"model\":\"" + FakeAiTransport.Model + "\",\"done\":true}");
                }

                // A fixed, varied pattern: mostly 0.5 to 4 seconds, some slow, some garbage, some never answered.
                number++;
                var kind = number % 20;
                var latency = 0.5 + ((number * 7) % 8) * 0.5;
                if (kind == 7)
                {
                    return null;
                }

                string content;
                if (kind == 3)
                {
                    content = "I'm not sure, what do you think?";
                }
                else if (call.Body.Contains(FakeAiTransport.DayPlanMarker))
                {
                    content = FakeAiTransport.DayPlan;
                }
                else if (call.Body.Contains(DialogueMarker))
                {
                    content = "{\"say\":\"Lovely day for it.\",\"tone\":\"" + (number % 3 == 0 ? "playful" : "warm") + "\",\"topic\":\"the weather\",\"end\":false}";
                }
                else if (call.Body.Contains("\"ratings\""))
                {
                    var ids = System.Text.RegularExpressions.Regex.Matches(call.Body, "mem-\\d{6}").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value).Distinct();
                    content = "{\"ratings\":[" + string.Join(",", ids.Select(id => "{\"id\":\"" + id + "\",\"importance\":3}")) + "]}";
                }
                else if (call.Body.Contains(ReflectionMarker))
                {
                    content = "{\"summary\":\"A quiet, pleasant day in the village.\",\"importance\":2}";
                }
                else
                {
                    content = Relax;
                }

                due.Add((call, now + (kind == 11 ? 30 : latency), FakeAiTransport.ChatReply(content)));
                return null;
            };

            var ai = new AiService(new OllamaSettings { timeoutSeconds = 20 }, transport);
            ai.Attach(sim);
            for (var i = 0; i < 3; i++)
            {
                ai.Update(now);
                now += 0.1;
            }

            Assert.That(ai.Status, Is.EqualTo(AiConnectionStatus.Connected));

            const double SecondsPerTick = 1.0; // 1x: a game minute each real second
            var longestDeciding = new Dictionary<string, int>();
            var worstDeciding = 0;
            var longestQueue = 0;
            var aiGoals = 0;
            for (var tick = 0; tick < 14 * GameTime.MinutesPerDay; tick++)
            {
                foreach (var d in due.Where(d => d.at <= now && !d.call.IsDone).ToList())
                {
                    d.call.Succeed(d.reply);
                }

                due.RemoveAll(d => d.call.IsDone);
                ai.Update(now);
                sim.Tick();
                now += SecondsPerTick;

                longestQueue = Math.Max(longestQueue, ai.Queue.PendingCount);
                aiGoals += sim.Residents.Count(r => r.GoalSource == DecisionSource.Ai);
                foreach (var r in sim.Residents)
                {
                    longestDeciding[r.Id] = r.State == ResidentState.Deciding ? (longestDeciding.TryGetValue(r.Id, out var m) ? m + 1 : 1) : 0;
                    worstDeciding = Math.Max(worstDeciding, longestDeciding[r.Id]);
                }

                if (tick % (6 * 60) == 0)
                {
                    var check = SaveSerializer.Load(SaveSerializer.Save(sim), FlatLand.CreateWorld());
                    Assert.That(check.Success, Is.True, string.Join("; ", check.Errors));
                    Assert.That(check.Simulation.Residents.Any(r => r.State == ResidentState.Deciding), Is.False, "unfinished AI decisions are never saved");
                }
            }

            var stats = ai.Queue.Stats;
            TestContext.WriteLine($"Sent {stats.Sent}, accepted {stats.Accepted}, timed out {stats.TimedOut}, invalid {stats.InvalidReplies}, stale {stats.Stale}, dropped {stats.Dropped}, longest queue {longestQueue}, average latency {stats.AverageLatencySeconds:0.00} s.");
            TestContext.WriteLine($"Longest time a resident spent waiting on the AI: {worstDeciding} game minutes.");

            Assert.That(ai.Status, Is.EqualTo(AiConnectionStatus.Connected), "a few bad replies shouldn't take the AI offline");
            Assert.That(stats.Accepted, Is.GreaterThan(500), "the AI should be doing most of the deciding and talking");
            Assert.That(longestQueue, Is.LessThanOrEqualTo((sim.Residents.Count * 6) + 1), "per resident at most a choice (or day plan or building sketch), a line or meeting turn, a reaction, a reflection, an insight and a memory rating, plus one embedding batch");
            Assert.That(worstDeciding, Is.LessThanOrEqualTo(MaxMinutesDeciding), "an answer that doesn't come is asked for again, so nobody waits for ever");
            Assert.That(aiGoals, Is.GreaterThan(0), "residents were seen following AI choices");
            Assert.That(sim.ActiveConversations.All(c => c.Lines.Count <= SocialRules.MaxLines), Is.True);
        }
    }

    internal static class SoakTestExtensions
    {
        public static IEnumerable<MemoryRecord> Memories(this Resident r, TownSimulation sim) => sim.Memories.ForResident(r.Id);
    }
}
