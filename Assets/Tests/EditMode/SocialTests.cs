using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.AI;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Persistence;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Social;
using TinyCity.Simulation.Time;
using TinyCity.Simulation.World;

namespace TinyCity.Tests.EditMode
{
    /// <summary>Phase 3: relationships, conversations, shared memories and interruptions, all without AI.</summary>
    public class SocialTests
    {
        private static Resident Mabel(TownSimulation sim) => sim.GetResident(StarterTown.ResidentId);

        private static Resident Arthur(TownSimulation sim) => sim.GetResident(StarterTown.ArthurId);

        /// <summary>Ticks until Mabel and Arthur are talking, or fails.</summary>
        private static ConversationSession StartChat(TownSimulation sim)
        {
            for (var i = 0; i < 5 && sim.ActiveConversations.Count == 0; i++)
            {
                sim.Tick();
            }

            Assert.That(sim.ActiveConversations.Count, Is.EqualTo(1), string.Join(" / ", Mabel(sim).Log.Select(e => e.Text)));
            return sim.ActiveConversations[0];
        }

        private static void FinishChat(TownSimulation sim)
        {
            for (var i = 0; i <= SocialRules.MaxConversationMinutes && sim.ActiveConversations.Count > 0; i++)
            {
                sim.Tick();
            }

            Assert.That(sim.ActiveConversations, Is.Empty);
        }

        [Test]
        public void LonelyNeighbours_StartTalking_AndBothAreInTheSameConversation()
        {
            var sim = TestTown.MabelAndArthurOnTheGreen(TestTown.WeekendAfternoon);

            var session = StartChat(sim);

            Assert.That(session.ParticipantIds, Is.EquivalentTo(new[] { StarterTown.ResidentId, StarterTown.ArthurId }));
            foreach (var r in new[] { Mabel(sim), Arthur(sim) })
            {
                Assert.That(r.State, Is.EqualTo(ResidentState.Talking));
                Assert.That(r.ConversationId, Is.EqualTo(session.Id));
                Assert.That(r.ActivityId, Is.EqualTo(ActivityCatalog.Chat.Id));
            }
        }

        [Test]
        public void Conversation_TakesTurns_StaysWithinTheGameLimits_AndIsRecorded()
        {
            var sim = TestTown.MabelAndArthurOnTheGreen(TestTown.WeekendAfternoon);
            var session = StartChat(sim);
            var started = sim.Time.TotalMinutes;

            FinishChat(sim);

            var record = sim.Conversations.All.Single(c => c.Id == session.Id);
            Assert.That(record.Kind, Is.EqualTo(ConversationKind.Chat));
            Assert.That(record.Lines.Count, Is.EqualTo(session.MaxLines));
            Assert.That(record.Lines.Count, Is.InRange(SocialRules.MinLines, SocialRules.MaxLines));
            for (var i = 1; i < record.Lines.Count; i++)
            {
                Assert.That(record.Lines[i].SpeakerId, Is.Not.EqualTo(record.Lines[i - 1].SpeakerId), "speakers alternate");
            }

            Assert.That(sim.Time.TotalMinutes - started, Is.LessThanOrEqualTo(SocialRules.MaxConversationMinutes));
            Assert.That(Mabel(sim).State, Is.Not.EqualTo(ResidentState.Talking));
            Assert.That(Arthur(sim).State, Is.Not.EqualTo(ResidentState.Talking));
            Assert.That(record.Lines.All(l => !l.FromAi), Is.True);
        }

        [Test]
        public void FirstChat_MakesThemAcquainted_AndGivesBothASharedMemory()
        {
            var sim = TestTown.MabelAndArthurOnTheGreen(TestTown.WeekendAfternoon);
            Assert.That(sim.Relationships.TryGet(StarterTown.ResidentId, StarterTown.ArthurId, out _), Is.False);
            var socialBefore = Mabel(sim).Needs[NeedType.Social];
            var session = StartChat(sim);

            FinishChat(sim);

            foreach (var (me, other) in new[] { (Mabel(sim), Arthur(sim)), (Arthur(sim), Mabel(sim)) })
            {
                var view = sim.Relationships.Get(me.Id, other.Id);
                Assert.That(view.HaveMet, Is.True);
                Assert.That(view.Interactions, Is.EqualTo(1));
                Assert.That(view.Friendship, Is.GreaterThan(10));
                var memory = sim.Memories.ForResident(me.Id).Single(m => m.Tags.Contains(session.Id));
                Assert.That(memory.Summary, Does.Contain("for the first time"));
                Assert.That(memory.Summary, Does.Contain(other.FullName));
                Assert.That(memory.Participants, Does.Contain(other.Id));
                Assert.That(memory.Importance, Is.EqualTo(5));
            }

            Assert.That(Mabel(sim).Needs[NeedType.Social], Is.GreaterThan(socialBefore));
        }

        [Test]
        public void SmallTalk_IsDeterministic()
        {
            var a = TestTown.MabelAndArthurOnTheGreen(TestTown.WeekendAfternoon);
            var b = TestTown.MabelAndArthurOnTheGreen(TestTown.WeekendAfternoon);
            a.Advance(6 * 60);
            b.Advance(6 * 60);

            Assert.That(a.Conversations.All, Is.Not.Empty);
            Assert.That(SaveSerializer.Save(a), Is.EqualTo(SaveSerializer.Save(b)));
        }

        [Test]
        public void UrgentNeed_EndsTheConversation_ForBoth()
        {
            var sim = TestTown.MabelAndArthurOnTheGreen(TestTown.WeekendAfternoon);
            var session = StartChat(sim);
            sim.Tick();
            Mabel(sim).Needs[NeedType.Thirst] = 5;

            sim.Tick();

            Assert.That(sim.ActiveConversations, Is.Empty);
            Assert.That(Mabel(sim).State, Is.Not.EqualTo(ResidentState.Talking));
            Assert.That(Arthur(sim).State, Is.Not.EqualTo(ResidentState.Talking));
            Assert.That(Mabel(sim).Log.Any(e => e.Text.StartsWith("Left the chat with Arthur: thirst")), Is.True);
            Assert.That(Arthur(sim).Log.Any(e => e.Text.StartsWith("Mabel had to go")), Is.True);
            Assert.That(sim.Conversations.All.Any(c => c.Id == session.Id), Is.True, "what was said is still recorded");
        }

        [Test]
        public void Bedtime_EndsTheConversation()
        {
            var sim = TestTown.MabelAndArthurOnTheGreen(GameTime.FromDayAndTime(6, 22, 26));
            StartChat(sim);

            sim.Advance(10);

            Assert.That(sim.ActiveConversations, Is.Empty);
            Assert.That(Mabel(sim).Log.Any(e => e.Text.Contains("Left the chat") || e.Text.Contains("had to go")), Is.True);
        }

        [Test]
        public void WorkTime_EndsTheConversation_ForTheWorker()
        {
            var shiftStart = GameTime.FromDayAndTime(1, 0, 0).AddMinutes(StarterTown.CreateResident(StarterTown.CreateWorld()).Job.StartMinuteOfDay - ActivityPlanner.CommuteLeadMinutes);
            var sim = TestTown.MabelAndArthurOnTheGreen(shiftStart.AddMinutes(-4));
            StartChat(sim);

            sim.Advance(6);

            Assert.That(sim.ActiveConversations, Is.Empty);
            Assert.That(Mabel(sim).Log.Any(e => e.Text == "Left the chat with Arthur: time for work"), Is.True);
        }

        [Test]
        public void AfterAChat_TheSamePairWaitsBeforeChattingAgain()
        {
            var sim = TestTown.MabelAndArthurOnTheGreen(TestTown.WeekendAfternoon);
            StartChat(sim);
            FinishChat(sim);
            var chats = sim.Conversations.All.Count(c => c.Kind == ConversationKind.Chat);

            for (var i = 0; i < SocialRules.PairCooldownMinutes - 5; i++)
            {
                Mabel(sim).Needs[NeedType.Social] = 40;
                Arthur(sim).Needs[NeedType.Social] = 40;
                sim.Tick();
            }

            Assert.That(sim.Conversations.All.Count(c => c.Kind == ConversationKind.Chat), Is.EqualTo(chats));
            Assert.That(sim.ActiveConversations, Is.Empty);
        }

        [Test]
        public void LonelyResident_GoesLookingForSomeone()
        {
            var sim = TestTown.WithResidents(
                TestTown.WeekendAfternoon,
                r => r.Id == StarterTown.ResidentId ? new Location(StarterTown.VillageMapId, 3, 8) : r.Id == StarterTown.ArthurId ? new Location(StarterTown.VillageMapId, 14, 9) : (Location?)null,
                (r, n) => n[NeedType.Social] = r.Id == StarterTown.ResidentId ? 30 : 90,
                r => r.Id == StarterTown.ResidentId || r.Id == StarterTown.ArthurId);

            sim.Tick();

            Assert.That(Mabel(sim).State, Is.EqualTo(ResidentState.Travelling));
            Assert.That(Mabel(sim).TargetResidentId, Is.EqualTo(StarterTown.ArthurId));
            Assert.That(Mabel(sim).Log.Last().Text, Does.StartWith("Went to find Arthur for a chat"));
        }

        [Test]
        public void NobodyIsApproachedIndoors()
        {
            var sim = TestTown.WithResidents(
                TestTown.WeekendAfternoon,
                r => r.Id == StarterTown.ResidentId ? new Location(StarterTown.VillageMapId, 10, 9) : (Location?)null,
                (r, n) => n[NeedType.Social] = 30,
                r => r.Id == StarterTown.ResidentId || r.Id == StarterTown.ArthurId);

            sim.Tick();

            Assert.That(sim.FreeTimeOptionsFor(Mabel(sim)).Any(o => o.Partner != null), Is.False, "Arthur is at home in Willow Cottage");
            Assert.That(Mabel(sim).TargetResidentId, Is.Null);
        }

        [Test]
        public void OtherPeoplesHomes_ArePrivate()
        {
            var sim = StarterTown.CreateNewGame();
            var mabel = Mabel(sim);
            var armchairInWillow = sim.World.Objects.First(o => o.Location.MapId == StarterTown.WillowCottageMapId && o.Type == ObjectType.Armchair);
            var armchairAtHome = sim.World.Objects.First(o => o.Location.MapId == StarterTown.CottageMapId && o.Type == ObjectType.Armchair);

            Assert.That(sim.Planner.IsAvailableTo(mabel, armchairInWillow), Is.False);
            Assert.That(sim.Planner.IsAvailableTo(mabel, armchairAtHome), Is.True);
            Assert.That(sim.Planner.IsAvailableTo(Arthur(sim), armchairInWillow), Is.True);
            Assert.That(sim.FreeTimeOptionsFor(mabel).Where(o => o.Target != null).All(o => o.Target.Location.MapId != StarterTown.WillowCottageMapId), Is.True);
        }

        [Test]
        public void FullObjects_AreNotOffered()
        {
            var sim = StarterTown.CreateNewGame();
            var tap = sim.World.Objects.First(o => o.Location.MapId == StarterTown.VillageMapId && o.Capacity == 1);
            var bench = sim.World.Objects.First(o => o.Type == ObjectType.Bench);
            Arthur(sim).State = ResidentState.Travelling;
            Arthur(sim).TargetObjectId = tap.Id;

            Assert.That(sim.Planner.IsAvailableTo(Mabel(sim), tap), Is.False, "one at a time");
            Assert.That(sim.Planner.IsAvailableTo(Arthur(sim), tap), Is.True, "you don't block yourself");

            Arthur(sim).TargetObjectId = bench.Id;
            Assert.That(sim.Planner.IsAvailableTo(Mabel(sim), bench), Is.True, "benches seat two");
            sim.GetResident(StarterTown.NellId).State = ResidentState.Performing;
            sim.GetResident(StarterTown.NellId).TargetObjectId = bench.Id;
            Assert.That(sim.Planner.IsAvailableTo(Mabel(sim), bench), Is.False);
        }

        [Test]
        public void CafeTables_SeatTwo_AndNewcomersPreferAnEmptyOne()
        {
            var window = TestTown.Object(StarterTown.CreateNewGame(), "obj-cafe-table-window");
            var sim = TestTown.WithResidents(TestTown.WeekendAfternoon, r => r.Id == StarterTown.ResidentId ? window.Location : (Location?)null, needsLevel: 60);
            var mabel = Mabel(sim);
            Assert.That(window.Capacity, Is.EqualTo(2), "one chair each side");

            string FirstTable() => sim.FreeTimeOptionsFor(mabel).First(o => o.Activity == ActivityCatalog.Coffee).Target.Id;
            Assert.That(FirstTable(), Is.EqualTo(window.Id), "the table she's standing at is nearest");

            Arthur(sim).State = ResidentState.Performing;
            Arthur(sim).TargetObjectId = window.Id;
            Assert.That(sim.Planner.IsAvailableTo(mabel, window), Is.True, "there's still a chair");
            Assert.That(FirstTable(), Is.Not.EqualTo(window.Id), "but an empty table is better than a stranger's");

            sim.GetResident(StarterTown.NellId).State = ResidentState.Travelling;
            sim.GetResident(StarterTown.NellId).TargetObjectId = window.Id;
            Assert.That(sim.Planner.IsAvailableTo(mabel, window), Is.False, "both chairs taken");
        }

        [Test]
        public void ToneChangesRelationships_ByTheGameRules()
        {
            var book = new RelationshipBook();
            var ab = book.Get("a", "b");
            var ba = book.Get("b", "a");
            var t = GameTime.FromDayAndTime(1, 12, 0);
            var lines = new List<ConversationLine>
            {
                new ConversationLine("a", t, "Lovely to see you!", ConversationTone.Warm),
                new ConversationLine("b", t, "Your hedge is a disgrace.", ConversationTone.Disagree),
            };

            SocialRules.ApplyConversation(ab, ba, lines, t);

            Assert.That(ba.Friendship, Is.EqualTo(10 + 1.5 + 1), "b liked a's warm words");
            Assert.That(ba.Trust, Is.EqualTo(20 + 0.5 + 0.5));
            Assert.That(ab.Dislike, Is.EqualTo(3), "a didn't like being told off");
            Assert.That(ab.Friendship, Is.EqualTo(10 + 0.5 - 1 + 1));
            Assert.That(ab.Interactions, Is.EqualTo(1));
            Assert.That(ab.LastInteractionMinute, Is.EqualTo(t.TotalMinutes));
            Assert.That(SocialRules.HadDisagreement(lines), Is.True);
        }

        [Test]
        public void RelationshipValues_StayWithinRange()
        {
            var book = new RelationshipBook();
            var ab = book.Get("a", "b");
            var ba = book.Get("b", "a");
            var t = GameTime.FromDayAndTime(1, 12, 0);
            for (var i = 0; i < 100; i++)
            {
                SocialRules.ApplyConversation(ab, ba, new[] { new ConversationLine("b", t, "Hmph.", ConversationTone.Disagree) }, t);
            }

            Assert.That(ab.Dislike, Is.EqualTo(Relationship.Max));
            Assert.That(ab.Friendship, Is.InRange(Relationship.Min, Relationship.Max));
            Assert.That(ab.Describe(), Is.EqualTo("doesn't get on with"));
            Assert.Throws<System.ArgumentException>(() => book.Get("a", "a"));
        }

        [Test]
        public void Disagreement_IsRemembered()
        {
            var sim = TestTown.MabelAndArthurOnTheGreen(TestTown.WeekendAfternoon);
            Meet(sim);
            sim.AiDecisionsEnabled = true;
            sim.Advance(SocialRules.PairCooldownMinutes);
            Mabel(sim).Needs[NeedType.Social] = 40;
            Arthur(sim).Needs[NeedType.Social] = 40;
            ConversationSession session = null;
            for (var i = 0; i < 10 && session == null; i++)
            {
                sim.Tick();
                session = sim.ActiveConversations.FirstOrDefault();
            }

            Assert.That(session, Is.Not.Null);
            while (sim.GetConversation(session.Id) != null)
            {
                if (session.AwaitingLine)
                {
                    var tone = session.SpeakerId == StarterTown.ArthurId ? "disagree" : "neutral";
                    Assert.That(sim.ApplyConversationLine(session.Id, session.LineRequestId, session.SpeakerId, "I don't agree at all.", tone, "the hedges", false), Is.EqualTo(DecisionOutcome.Applied));
                }

                sim.Tick();
            }

            var memory = sim.Memories.ForResident(StarterTown.ResidentId).Last();
            Assert.That(memory.Summary, Is.EqualTo("Arthur and I didn't see eye to eye about the hedges."));
            Assert.That(sim.Relationships.Get(StarterTown.ResidentId, StarterTown.ArthurId).Dislike, Is.GreaterThan(0));
        }

        /// <summary>Lets Mabel and Arthur have their first chat with built-in small talk.</summary>
        private static void Meet(TownSimulation sim)
        {
            StartChat(sim);
            FinishChat(sim);
        }

        [Test]
        public void AiLines_AreCheckedBeforeTheyCount()
        {
            var sim = TestTown.MabelAndArthurOnTheGreen(TestTown.WeekendAfternoon);
            sim.AiDecisionsEnabled = true;
            var session = StartChat(sim);
            while (!session.AwaitingLine)
            {
                sim.Tick();
            }

            var id = session.LineRequestId;
            var speaker = session.SpeakerId;
            var listener = session.ListenerId;
            Assert.That(sim.ApplyConversationLine(session.Id, id + 1, speaker, "Hello!", "warm", null, false), Is.EqualTo(DecisionOutcome.Stale), "old or future request");
            Assert.That(sim.ApplyConversationLine(session.Id, id, listener, "Hello!", "warm", null, false), Is.EqualTo(DecisionOutcome.Stale), "not their turn");
            Assert.That(sim.ApplyConversationLine("conv-999999", id, speaker, "Hello!", "warm", null, false), Is.EqualTo(DecisionOutcome.Stale), "no such conversation");
            Assert.That(sim.ApplyConversationLine(session.Id, id, speaker, "   ", "warm", null, false), Is.EqualTo(DecisionOutcome.InvalidText));
            Assert.That(session.Lines, Is.Empty);

            Assert.That(sim.ApplyConversationLine(session.Id, id, speaker, "Well hello there!", "smug", "  the weather!  ", true), Is.EqualTo(DecisionOutcome.Applied));
            Assert.That(session.Lines.Single().Tone, Is.EqualTo(ConversationTone.Neutral), "unknown tones count as neutral");
            Assert.That(session.Lines.Single().FromAi, Is.True);
            Assert.That(session.Topic, Is.EqualTo("the weather"));
            Assert.That(sim.GetConversation(session.Id), Is.Not.Null, "can't end before the minimum length");
            Assert.That(sim.ApplyConversationLine(session.Id, id, speaker, "Again!", "warm", null, false), Is.EqualTo(DecisionOutcome.Stale), "each request is answered once");
        }

        [Test]
        public void LateAiLine_FallsBackToSmallTalk()
        {
            var sim = TestTown.MabelAndArthurOnTheGreen(TestTown.WeekendAfternoon);
            sim.AiDecisionsEnabled = true;
            var session = StartChat(sim);
            while (!session.AwaitingLine)
            {
                sim.Tick();
            }

            var id = session.LineRequestId;
            sim.Advance(SocialRules.LineWaitMinutes);

            Assert.That(session.Lines.Count, Is.EqualTo(1));
            Assert.That(session.Lines[0].FromAi, Is.False);
            Assert.That(sim.ApplyConversationLine(session.Id, id, session.Lines[0].SpeakerId, "Sorry I'm late!", "warm", null, false), Is.EqualTo(DecisionOutcome.Stale));
        }

        [Test]
        public void AiCanEndAChatEarly_OnceTheMinimumIsSaid()
        {
            var sim = TestTown.MabelAndArthurOnTheGreen(TestTown.WeekendAfternoon);
            sim.AiDecisionsEnabled = true;
            var session = StartChat(sim);
            var said = 0;
            while (sim.GetConversation(session.Id) != null)
            {
                if (session.AwaitingLine)
                {
                    sim.ApplyConversationLine(session.Id, session.LineRequestId, session.SpeakerId, $"Line {++said}", "playful", "gardens", true);
                }

                sim.Tick();
            }

            Assert.That(said, Is.EqualTo(SocialRules.MinLines));
            var record = sim.Conversations.All.Single(c => c.Id == session.Id);
            Assert.That(record.Lines.All(l => l.FromAi && l.Tone == ConversationTone.Playful), Is.True);
            Assert.That(record.Context, Is.EqualTo("gardens"));
        }

        [Test]
        public void SaveDuringAChat_LoadsAndCarriesOnExactly()
        {
            var world = StarterTown.CreateWorld();
            var sim = TestTown.MabelAndArthurOnTheGreen(TestTown.WeekendAfternoon);
            var session = StartChat(sim);
            sim.Advance(3);
            Assert.That(session.Lines, Is.Not.Empty);

            var saved = SaveSerializer.Save(sim);
            var result = SaveSerializer.Load(saved, world);

            Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.Warnings, Is.Empty, string.Join("; ", result.Warnings));
            var loaded = result.Simulation;
            var restored = loaded.GetConversation(session.Id);
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.Lines.Select(l => l.Text), Is.EqualTo(session.Lines.Select(l => l.Text)));
            Assert.That(restored.MaxLines, Is.EqualTo(session.MaxLines));
            Assert.That(Mabel(loaded).State, Is.EqualTo(ResidentState.Talking));
            Assert.That(SaveSerializer.Save(loaded), Is.EqualTo(saved));

            sim.Advance(120);
            loaded.Advance(120);
            Assert.That(SaveSerializer.Save(loaded), Is.EqualTo(SaveSerializer.Save(sim)));
            Assert.That(loaded.Relationships.Get(StarterTown.ResidentId, StarterTown.ArthurId).HaveMet, Is.True);
        }

        [Test]
        public void RelationshipsAndLineTones_SurviveSaveAndLoad()
        {
            var sim = TestTown.MabelAndArthurOnTheGreen(TestTown.WeekendAfternoon);
            sim.AiDecisionsEnabled = true;
            var session = StartChat(sim);
            while (sim.GetConversation(session.Id) != null)
            {
                if (session.AwaitingLine)
                {
                    sim.ApplyConversationLine(session.Id, session.LineRequestId, session.SpeakerId, "Ha! You don't say.", "playful", "cake", false);
                }

                sim.Tick();
            }

            var loaded = SaveSerializer.Load(SaveSerializer.Save(sim), StarterTown.CreateWorld()).Simulation;

            foreach (var rel in sim.Relationships.All)
            {
                var copy = loaded.Relationships.Get(rel.OwnerId, rel.OtherId);
                Assert.That(copy.Friendship, Is.EqualTo(rel.Friendship));
                Assert.That(copy.Trust, Is.EqualTo(rel.Trust));
                Assert.That(copy.Respect, Is.EqualTo(rel.Respect));
                Assert.That(copy.Dislike, Is.EqualTo(rel.Dislike));
                Assert.That(copy.Interactions, Is.EqualTo(rel.Interactions));
                Assert.That(copy.LastInteractionMinute, Is.EqualTo(rel.LastInteractionMinute));
            }

            var line = loaded.Conversations.All.Single(c => c.Id == session.Id).Lines[0];
            Assert.That(line.Tone, Is.EqualTo(ConversationTone.Playful));
            Assert.That(line.FromAi, Is.True);
        }

        [Test]
        public void WaitingForAnAiLine_IsNotSaved()
        {
            var sim = TestTown.MabelAndArthurOnTheGreen(TestTown.WeekendAfternoon);
            sim.AiDecisionsEnabled = true;
            var session = StartChat(sim);
            while (!session.AwaitingLine)
            {
                sim.Tick();
            }

            sim.Advance(3);
            Assert.That(session.WaitedMinutes, Is.EqualTo(3));
            var loaded = SaveSerializer.Load(SaveSerializer.Save(sim), StarterTown.CreateWorld()).Simulation;

            var restored = loaded.GetConversation(session.Id);
            Assert.That(restored.WaitedMinutes, Is.EqualTo(3), "waiting time still doesn't count towards the chat's length");
            Assert.That(restored.AwaitingLine, Is.False);
            Assert.That(restored.LineRequestId, Is.EqualTo(0));
            loaded.Tick();
            Assert.That(restored.Lines.Count, Is.EqualTo(1), "without the AI the line is small talk");
        }

        [Test]
        public void SaveWhileLookingForSomeone_KeepsLooking()
        {
            var sim = TestTown.WithResidents(
                TestTown.WeekendAfternoon,
                r => r.Id == StarterTown.ResidentId ? new Location(StarterTown.VillageMapId, 3, 8) : r.Id == StarterTown.ArthurId ? new Location(StarterTown.VillageMapId, 14, 9) : (Location?)null,
                (r, n) => n[NeedType.Social] = r.Id == StarterTown.ResidentId ? 30 : 90,
                r => r.Id == StarterTown.ResidentId || r.Id == StarterTown.ArthurId);
            sim.Advance(2);
            Assert.That(Mabel(sim).TargetResidentId, Is.EqualTo(StarterTown.ArthurId));

            var loaded = SaveSerializer.Load(SaveSerializer.Save(sim), StarterTown.CreateWorld()).Simulation;

            Assert.That(Mabel(loaded).State, Is.EqualTo(ResidentState.Travelling));
            Assert.That(Mabel(loaded).TargetResidentId, Is.EqualTo(StarterTown.ArthurId));
            Assert.That(Mabel(loaded).MinutesRemaining, Is.EqualTo(Mabel(sim).MinutesRemaining));
        }

        [Test]
        public void DamagedSocialData_IsRepairedWithWarnings()
        {
            var sim = TestTown.MabelAndArthurOnTheGreen(TestTown.WeekendAfternoon);
            StartChat(sim);
            sim.Advance(2);
            var root = (Dictionary<string, object>)Json.Parse(SaveSerializer.Save(sim));
            var conversations = (List<object>)root["activeConversations"];
            ((Dictionary<string, object>)conversations[0])["participants"] = new List<object> { StarterTown.ResidentId, "res-ghost" };
            var relationships = (List<object>)root["relationships"];
            relationships.Add(new Dictionary<string, object> { ["ownerId"] = StarterTown.ResidentId, ["otherId"] = "res-ghost", ["friendship"] = 90.0 });
            relationships.Add(new Dictionary<string, object> { ["ownerId"] = StarterTown.ResidentId, ["otherId"] = StarterTown.ResidentId });

            var result = SaveSerializer.Load(Json.Write(root), StarterTown.CreateWorld());

            Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));
            var loaded = result.Simulation;
            Assert.That(loaded.ActiveConversations, Is.Empty);
            Assert.That(Mabel(loaded).State, Is.Not.EqualTo(ResidentState.Talking));
            Assert.That(Arthur(loaded).State, Is.Not.EqualTo(ResidentState.Talking));
            Assert.That(loaded.Relationships.All.Count, Is.EqualTo(sim.Relationships.All.Count));
            Assert.That(result.Warnings.Any(w => w.Contains("2 damaged or duplicate relationship")), Is.True, string.Join("; ", result.Warnings));
            Assert.That(result.Warnings.Any(w => w.Contains("conversation")), Is.True, string.Join("; ", result.Warnings));
            loaded.Advance(60);
        }

        [Test]
        public void Version2Save_Upgrades_AndTheNewNeighboursMoveIn()
        {
            var sim = TestTown.WithResident(TestTown.WeekendAfternoon);
            StarterTown.AddLifeHistory(sim);
            var root = (Dictionary<string, object>)Json.Parse(SaveSerializer.Save(sim));
            root["version"] = 2.0;
            root.Remove("relationships");
            root.Remove("activeConversations");
            foreach (var r in ((List<object>)root["residents"]).Cast<Dictionary<string, object>>())
            {
                r.Remove("targetResidentId");
                r.Remove("conversationId");
            }

            var result = SaveSerializer.Load(Json.Write(root), StarterTown.CreateWorld());

            Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.Warnings, Is.Empty, string.Join("; ", result.Warnings));
            Assert.That(result.UpgradedFromVersion, Is.EqualTo(2));
            var loaded = result.Simulation;
            Assert.That(loaded.Relationships.All, Is.Empty);
            var everyone = StarterTown.CreateResidents(StarterTown.CreateWorld()).Select(r => r.Id).ToList();
            Assert.That(StarterTown.AddMissingResidents(loaded), Is.EqualTo(everyone.Count - 1));
            Assert.That(StarterTown.AddMissingResidents(loaded), Is.EqualTo(0));
            Assert.That(loaded.Residents.Select(r => r.Id).ToList(), Is.EqualTo(everyone));
            Assert.That(loaded.Memories.ForResident(StarterTown.NellId), Is.Not.Empty);
            Assert.That(loaded.Memories.All.Select(m => m.Id).Distinct().Count(), Is.EqualTo(loaded.Memories.All.Count));

            loaded.Advance(24 * 60);
            var resaved = SaveSerializer.Load(SaveSerializer.Save(loaded), StarterTown.CreateWorld());
            Assert.That(resaved.Success, Is.True);
            Assert.That(resaved.Simulation.Residents.Count, Is.EqualTo(everyone.Count));
        }

        [Test]
        public void EveryResident_CanReachEveryPlaceTheyMayUse()
        {
            var sim = StarterTown.CreateNewGame();
            foreach (var r in sim.Residents)
            {
                Assert.That(sim.World.TryGetObject(r.BedObjectId, out var bed), Is.True);
                Assert.That(bed.Location.MapId, Is.EqualTo(r.HomeMapId));
                foreach (var obj in sim.World.Objects.Where(o => sim.Planner.IsAvailableTo(r, o)))
                {
                    Assert.That(sim.Pathfinder.Distance(bed.Location, obj.Location), Is.Not.EqualTo(Pathfinder.Unreachable), $"{r.FirstName} can't reach {obj.Name}");
                }

                if (r.Job != null)
                {
                    Assert.That(sim.World.TryGetObject(r.Job.WorkObjectId, out var work), Is.True);
                    Assert.That(ActivityCatalog.Work.CanUse(work), Is.True, $"{r.FirstName} can work at {work.Name}");
                }
            }

            Assert.That(sim.Residents.Select(r => r.Id).Distinct().Count(), Is.EqualTo(sim.Residents.Count));
            Assert.That(sim.Residents.Select(r => r.BedObjectId).Distinct().Count(), Is.EqualTo(sim.Residents.Count));
        }

        [Test]
        public void AWeekInTheVillage_EveryoneMeetsEveryone()
        {
            // Eight days: since residents walk around furniture, the last pair (Priya and Samuel) first meet on day 8.
            var sim = StarterTown.CreateNewGame();
            sim.Advance(8 * 24 * 60);

            foreach (var r in sim.Residents)
            {
                foreach (var other in sim.Residents.Where(o => o != r))
                {
                    Assert.That(sim.Relationships.Get(r.Id, other.Id).HaveMet, Is.True, $"{r.FirstName} never met {other.FirstName}");
                }

                Assert.That(r.Needs[NeedType.Social], Is.GreaterThan(20), $"{r.FirstName} is lonely");
                Assert.That(r.State, Is.Not.EqualTo(ResidentState.Talking), "nobody chats through the night");
            }
        }
    }
}
