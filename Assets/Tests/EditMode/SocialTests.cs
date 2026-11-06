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
    /// <summary>Relationships, conversations, shared memories and interruptions, with a stand-in AI writing the lines.</summary>
    public class SocialTests
    {
        private static Resident Mabel(TownSimulation sim) => sim.GetResident(StarterResidents.ResidentId);

        private static Resident Arthur(TownSimulation sim) => sim.GetResident(StarterResidents.ArthurId);

        /// <summary>Mabel goes over to Arthur and they start talking.</summary>
        private static ConversationSession StartChat(TownSimulation sim)
        {
            var session = TestTown.StartChat(sim);
            Assert.That(sim.ActiveConversations.Count, Is.EqualTo(1));
            return session;
        }

        /// <summary>Runs until the chat is over, with a stand-in AI writing every line.</summary>
        private static void FinishChat(TownSimulation sim)
        {
            for (var i = 0; i <= SocialRules.MaxConversationMinutes * 2 && sim.ActiveConversations.Count > 0; i++)
            {
                TestTown.Answer(sim);
                sim.Tick();
            }

            Assert.That(sim.ActiveConversations, Is.Empty);
        }

        [Test]
        public void LonelyNeighbours_StartTalking_AndBothAreInTheSameConversation()
        {
            var sim = TestTown.TwoSideBySide(TestTown.WeekendAfternoon);

            var session = StartChat(sim);

            Assert.That(session.ParticipantIds, Is.EquivalentTo(new[] { StarterResidents.ResidentId, StarterResidents.ArthurId }));
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
            var sim = TestTown.TwoSideBySide(TestTown.WeekendAfternoon);
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
            Assert.That(record.Lines.All(l => l.FromAi), Is.True, "every line is written by the AI");
        }

        [Test]
        public void FirstChat_MakesThemAcquainted_AndGivesBothASharedMemory()
        {
            var sim = TestTown.TwoSideBySide(TestTown.WeekendAfternoon);
            Assert.That(sim.Relationships.TryGet(StarterResidents.ResidentId, StarterResidents.ArthurId, out _), Is.False);
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
        public void UrgentNeed_EndsTheConversation_ForBoth()
        {
            var sim = TestTown.TwoSideBySide(TestTown.WeekendAfternoon);
            var session = StartChat(sim);
            sim.Tick();
            TestTown.Answer(sim);
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
        public void AfterAChat_TheSamePairWaitsBeforeChattingAgain()
        {
            var sim = TestTown.TwoSideBySide(TestTown.WeekendAfternoon);
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
        public void FullObjects_AreNotOffered()
        {
            var sim = FlatLand.CreateNewGame();
            var tap = sim.World.Objects.First(o => o.Location.MapId == FlatLand.MapId && o.Capacity == 1);
            var bench = sim.World.Objects.First(o => o.Type == ObjectType.Bench);
            Arthur(sim).State = ResidentState.Travelling;
            Arthur(sim).TargetObjectId = tap.Id;

            Assert.That(sim.Planner.IsAvailableTo(Mabel(sim), tap), Is.False, "one at a time");
            Assert.That(sim.Planner.IsAvailableTo(Arthur(sim), tap), Is.True, "you don't block yourself");

            Arthur(sim).TargetObjectId = bench.Id;
            Assert.That(sim.Planner.IsAvailableTo(Mabel(sim), bench), Is.True, "benches seat two");
            sim.GetResident(StarterResidents.NellId).State = ResidentState.Performing;
            sim.GetResident(StarterResidents.NellId).TargetObjectId = bench.Id;
            Assert.That(sim.Planner.IsAvailableTo(Mabel(sim), bench), Is.False);
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
            var sim = TestTown.TwoSideBySide(TestTown.WeekendAfternoon);
            Meet(sim);
            sim.AiDecisionsEnabled = true;
            sim.Advance(SocialRules.PairCooldownMinutes);
            var session = StartChat(sim);
            while (sim.GetConversation(session.Id) != null)
            {
                if (session.AwaitingLine)
                {
                    var tone = session.SpeakerId == StarterResidents.ArthurId ? "disagree" : "neutral";
                    Assert.That(sim.ApplyConversationLine(session.Id, session.LineRequestId, session.SpeakerId, "I don't agree at all.", tone, "the hedges", false), Is.EqualTo(DecisionOutcome.Applied));
                }

                sim.Tick();
            }

            var memory = sim.Memories.ForResident(StarterResidents.ResidentId).Last();
            Assert.That(memory.Summary, Is.EqualTo("Arthur and I didn't see eye to eye about the hedges."));
            Assert.That(sim.Relationships.Get(StarterResidents.ResidentId, StarterResidents.ArthurId).Dislike, Is.GreaterThan(0));
        }

        /// <summary>Lets Mabel and Arthur have their first chat, with a stand-in AI writing the lines.</summary>
        private static void Meet(TownSimulation sim)
        {
            StartChat(sim);
            FinishChat(sim);
        }

        [Test]
        public void AiLines_AreCheckedBeforeTheyCount()
        {
            var sim = TestTown.TwoSideBySide(TestTown.WeekendAfternoon);
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
        public void AiCanEndAChatEarly_OnceTheMinimumIsSaid()
        {
            var sim = TestTown.TwoSideBySide(TestTown.WeekendAfternoon);
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
            var world = FlatLand.CreateWorld();
            var sim = TestTown.TwoSideBySide(TestTown.WeekendAfternoon);
            var session = StartChat(sim);
            for (var i = 0; i < 3; i++)
            {
                TestTown.Answer(sim);
                sim.Tick();
            }

            // Answered first, so the save isn't halfway through a line (that is asked for again after loading).
            TestTown.Answer(sim);
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

            loaded.AiDecisionsEnabled = sim.AiDecisionsEnabled;
            for (var i = 0; i < 120; i++)
            {
                TestTown.Answer(sim);
                sim.Tick();
                TestTown.Answer(loaded);
                loaded.Tick();
            }

            Assert.That(SaveSerializer.Save(loaded), Is.EqualTo(SaveSerializer.Save(sim)));
            Assert.That(loaded.Relationships.Get(StarterResidents.ResidentId, StarterResidents.ArthurId).HaveMet, Is.True);
        }

        [Test]
        public void RelationshipsAndLineTones_SurviveSaveAndLoad()
        {
            var sim = TestTown.TwoSideBySide(TestTown.WeekendAfternoon);
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

            var loaded = SaveSerializer.Load(SaveSerializer.Save(sim), FlatLand.CreateWorld()).Simulation;

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
            var sim = TestTown.TwoSideBySide(TestTown.WeekendAfternoon);
            sim.AiDecisionsEnabled = true;
            var session = StartChat(sim);
            while (!session.AwaitingLine)
            {
                sim.Tick();
            }

            sim.Advance(3);
            Assert.That(session.WaitedMinutes, Is.EqualTo(3));
            var loaded = SaveSerializer.Load(SaveSerializer.Save(sim), FlatLand.CreateWorld()).Simulation;

            var restored = loaded.GetConversation(session.Id);
            Assert.That(restored.WaitedMinutes, Is.EqualTo(3), "waiting time still doesn't count towards the chat's length");
            Assert.That(restored.AwaitingLine, Is.False);
            Assert.That(restored.LineRequestId, Is.EqualTo(0));
            loaded.Tick();
            Assert.That(restored.AwaitingLine, Is.True, "the line is asked for again");
            Assert.That(restored.LineRequestId, Is.GreaterThan(0));
            Assert.That(restored.Lines, Is.Empty, "nothing is said until the AI writes it");
        }

        [Test]
        public void SaveWhileLookingForSomeone_KeepsLooking()
        {
            var sim = TestTown.WithResidents(
                TestTown.WeekendAfternoon,
                r => r.Id == StarterResidents.ResidentId ? TestTown.At(30, 8) : r.Id == StarterResidents.ArthurId ? TestTown.At(44, 9) : (Location?)null,
                include: r => r.Id == StarterResidents.ResidentId || r.Id == StarterResidents.ArthurId);
            sim.AiDecisionsEnabled = true;
            sim.Tick();
            var chat = sim.FreeTimeOptionsFor(Mabel(sim)).Single(o => o.Partner == Arthur(sim));
            Assert.That(sim.ApplyDecision(Mabel(sim).Id, Mabel(sim).PendingDecisionId, chat.ActionId, chat.TargetId, "Company"), Is.EqualTo(DecisionOutcome.Applied));
            sim.Advance(2);
            Assert.That(Mabel(sim).TargetResidentId, Is.EqualTo(StarterResidents.ArthurId));

            var loaded = SaveSerializer.Load(SaveSerializer.Save(sim), FlatLand.CreateWorld()).Simulation;

            Assert.That(Mabel(loaded).State, Is.EqualTo(ResidentState.Travelling));
            Assert.That(Mabel(loaded).TargetResidentId, Is.EqualTo(StarterResidents.ArthurId));
            Assert.That(Mabel(loaded).MinutesRemaining, Is.EqualTo(Mabel(sim).MinutesRemaining));
        }

        [Test]
        public void DamagedSocialData_IsRepairedWithWarnings()
        {
            var sim = TestTown.TwoSideBySide(TestTown.WeekendAfternoon);
            StartChat(sim);
            sim.Advance(2);
            var root = (Dictionary<string, object>)Json.Parse(SaveSerializer.Save(sim));
            var conversations = (List<object>)root["activeConversations"];
            ((Dictionary<string, object>)conversations[0])["participants"] = new List<object> { StarterResidents.ResidentId, "res-ghost" };
            var relationships = (List<object>)root["relationships"];
            relationships.Add(new Dictionary<string, object> { ["ownerId"] = StarterResidents.ResidentId, ["otherId"] = "res-ghost", ["friendship"] = 90.0 });
            relationships.Add(new Dictionary<string, object> { ["ownerId"] = StarterResidents.ResidentId, ["otherId"] = StarterResidents.ResidentId });

            var result = SaveSerializer.Load(Json.Write(root), FlatLand.CreateWorld());

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
    }
}
