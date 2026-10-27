using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using TinyTown.Simulation;
using TinyTown.Simulation.Activities;
using TinyTown.Simulation.AI;
using TinyTown.Simulation.Configuration;
using TinyTown.Simulation.Content;
using TinyTown.Simulation.Economy;
using TinyTown.Simulation.History;
using TinyTown.Simulation.Memory;
using TinyTown.Simulation.Persistence;
using TinyTown.Simulation.Residents;
using TinyTown.Simulation.Social;
using TinyTown.Simulation.Time;

namespace TinyTown.Tests.EditMode
{
    /// <summary>
    /// Long runs of the whole town, checking the Phase 4 acceptance criteria: no corrupted saves, no uncontrolled
    /// queues or collections, and no blocked critical needs.
    /// </summary>
    public class SoakTests
    {
        /// <summary>Six in-game months (six 28-day years of resident ageing).</summary>
        private const int SoakDays = 168;

        /// <summary>The longest a living resident may go with a critical need before something is done about it.</summary>
        private const int MaxMinutesCritical = 6 * 60;

        private static readonly NeedType[] UrgentNeeds = { NeedType.Hunger, NeedType.Thirst, NeedType.Toilet, NeedType.Energy };

        [Test]
        public void SixMonths_SavedAndReloadedEveryWeek_MatchesAnUninterruptedRun_AndStaysHealthy()
        {
            var world = StarterTown.CreateWorld();
            var straight = StarterTown.CreateNewGame();
            var viaSaves = StarterTown.CreateNewGame();
            var critical = new Dictionary<string, int>();
            var worstCritical = 0;
            var timer = Stopwatch.StartNew();
            long tickTicks = 0;

            for (var day = 1; day <= SoakDays; day++)
            {
                for (var minute = 0; minute < GameTime.MinutesPerDay; minute++)
                {
                    var before = Stopwatch.GetTimestamp();
                    straight.Tick();
                    tickTicks += Stopwatch.GetTimestamp() - before;
                    viaSaves.Tick();

                    foreach (var r in straight.Residents.Where(r => r.IsAlive))
                    {
                        var isCritical = UrgentNeeds.Any(n => r.Needs[n] < NeedRules.Critical);
                        critical[r.Id] = isCritical ? (critical.TryGetValue(r.Id, out var c) ? c + 1 : 1) : 0;
                        worstCritical = Math.Max(worstCritical, critical[r.Id]);
                    }
                }

                CheckInvariants(straight);
                if (day % 7 == 0)
                {
                    var saved = SaveSerializer.Save(viaSaves);
                    var result = SaveSerializer.Load(saved, world);
                    Assert.That(result.Success, Is.True, $"day {day}: " + string.Join("; ", result.Errors));
                    Assert.That(result.Warnings, Is.Empty, $"day {day}: " + string.Join("; ", result.Warnings));
                    viaSaves = result.Simulation;
                    Assert.That(SaveSerializer.Save(viaSaves), Is.EqualTo(saved), $"day {day}: a save loads back exactly");
                    Assert.That(saved, Is.EqualTo(SaveSerializer.Save(straight)), $"day {day}: saving and loading changed the town's future");
                }
            }

            var msPerDay = tickTicks * 1000.0 / Stopwatch.Frequency / SoakDays;
            TestContext.WriteLine($"Simulated {SoakDays} days in {timer.Elapsed.TotalSeconds:0.0} s (both runs, with saves); one run's ticks average {msPerDay:0.0} ms per game day.");
            TestContext.WriteLine($"Longest time any resident spent with a critical need: {worstCritical} minutes.");
            TestContext.WriteLine($"Memories {straight.Memories.All.Count}, chats {straight.Conversations.All.Count}, ledger {straight.Economy.Ledger.All.Count}, history {straight.History.Events.Count}, couples {straight.Relationships.Partnerships.Count}, deaths {straight.Residents.Count(r => !r.IsAlive)}.");

            Assert.That(worstCritical, Is.LessThanOrEqualTo(MaxMinutesCritical), "a critical need was left unmet for too long");
            Assert.That(straight.History.Events.Count(e => e.Kind == TownEventKind.Gathering), Is.GreaterThanOrEqualTo(40), "two community events a week");
            Assert.That(straight.Residents.Count(r => r.IsAlive), Is.GreaterThanOrEqualTo(5), "the town shouldn't empty in six months");
        }

        /// <summary>Things that must hold at any moment, however long the town runs.</summary>
        private static void CheckInvariants(TownSimulation sim)
        {
            var label = $"day {sim.Time.Day}";
            Assert.That(sim.Residents.All(r => r.Memories(sim).Count() <= MemoryStore.MaxPerResident), Is.True, $"{label}: memories are capped");
            Assert.That(sim.Residents.All(r => r.Log.Count <= Resident.MaxLogEntries), Is.True, $"{label}: logs are capped");
            Assert.That(sim.Economy.Ledger.All.Count, Is.LessThanOrEqualTo(Ledger.MaxRecords), label);
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
                Assert.That(r.Groceries, Is.GreaterThanOrEqualTo(0), label);
                if (!r.IsAlive)
                {
                    Assert.That(r.State, Is.EqualTo(ResidentState.Idle), $"{label}: {r.FirstName} has died and does nothing");
                    Assert.That(r.ConversationId, Is.Null, label);
                    Assert.That(sim.Relationships.PartnerOf(r.Id), Is.Null, $"{label}: {r.FirstName} has died and is no longer in a couple");
                }
            }

            foreach (var couple in sim.Relationships.Partnerships)
            {
                var a = sim.GetResident(couple.FirstId);
                var b = sim.GetResident(couple.SecondId);
                Assert.That(RomanceRules.Compatible(a, b), Is.True, $"{label}: {a.FirstName} and {b.FirstName} are a couple but not compatible");
            }
        }

        // ---- AI under load ----

        private const string DialogueMarker = "You are chatting with";
        private const string ReflectionMarker = "diary-style memory";
        private const string Relax = "{\"action\":\"relax\",\"target_id\":\"none\",\"reason\":\"Just resting\",\"say\":\"Ahh.\"}";

        [Test]
        public void TwoWeeksAtFourTimesSpeed_WithASlowUnreliableAi_KeepsTheQueueBounded_AndNobodyStuck()
        {
            var sim = StarterTown.CreateNewGame();
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
                else if (call.Body.Contains(DialogueMarker))
                {
                    content = "{\"say\":\"Lovely day for it.\",\"tone\":\"" + (number % 3 == 0 ? "playful" : "warm") + "\",\"topic\":\"the weather\",\"end\":false}";
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

            const double SecondsPerTick = 0.25; // 4x speed: four game minutes each real second
            var longestDeciding = new Dictionary<string, int>();
            var worstDeciding = 0;
            var longestQueue = 0;
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
                foreach (var r in sim.Residents)
                {
                    longestDeciding[r.Id] = r.State == ResidentState.Deciding ? (longestDeciding.TryGetValue(r.Id, out var m) ? m + 1 : 1) : 0;
                    worstDeciding = Math.Max(worstDeciding, longestDeciding[r.Id]);
                }

                if (tick % (6 * 60) == 0)
                {
                    var check = SaveSerializer.Load(SaveSerializer.Save(sim), StarterTown.CreateWorld());
                    Assert.That(check.Success, Is.True, string.Join("; ", check.Errors));
                    Assert.That(check.Simulation.Residents.Any(r => r.State == ResidentState.Deciding), Is.False, "unfinished AI decisions are never saved");
                }
            }

            var stats = ai.Queue.Stats;
            TestContext.WriteLine($"Sent {stats.Sent}, accepted {stats.Accepted}, timed out {stats.TimedOut}, invalid {stats.InvalidReplies}, stale {stats.Stale}, dropped {stats.Dropped}, longest queue {longestQueue}, average latency {stats.AverageLatencySeconds:0.00} s.");
            TestContext.WriteLine($"Longest time a resident spent waiting on the AI: {worstDeciding} game minutes.");

            Assert.That(ai.Status, Is.EqualTo(AiConnectionStatus.Connected), "a few bad replies shouldn't take the AI offline");
            Assert.That(stats.Accepted, Is.GreaterThan(500), "the AI should be doing most of the deciding and talking");
            Assert.That(longestQueue, Is.LessThanOrEqualTo(sim.Residents.Count * 2), "one decision and one line or reflection per resident at most");
            Assert.That(worstDeciding, Is.LessThanOrEqualTo(sim.DecisionWaitMinutes + 1), "nobody waits on the AI past the deadline");
            Assert.That(sim.Residents.Count(r => r.GoalSource == DecisionSource.Ai), Is.GreaterThan(0));
            Assert.That(sim.ActiveConversations.All(c => c.Lines.Count <= SocialRules.MaxLines), Is.True);
        }
    }

    internal static class SoakTestExtensions
    {
        public static IEnumerable<MemoryRecord> Memories(this Resident r, TownSimulation sim) => sim.Memories.ForResident(r.Id);
    }
}
