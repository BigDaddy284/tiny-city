using System.Linq;
using NUnit.Framework;
using TinyTown.Simulation;
using TinyTown.Simulation.AI;
using TinyTown.Simulation.Configuration;
using TinyTown.Simulation.Content;
using TinyTown.Simulation.Memory;
using TinyTown.Simulation.Residents;
using TinyTown.Simulation.Social;
using TinyTown.Simulation.World;

namespace TinyTown.Tests.EditMode
{
    /// <summary>AI-written conversation lines through the real queue and service, against a scripted Ollama.</summary>
    public class AiDialogueTests
    {
        private const string DialogueMarker = "You are chatting with";
        private const string Relax = "{\"action\":\"relax\",\"target_id\":\"none\",\"reason\":\"Just resting\",\"say\":\"Ahh.\"}";

        private double now;

        [SetUp]
        public void SetUp() => now = 0;

        private AiService Connected(TownSimulation sim, FakeAiTransport transport)
        {
            var ai = new AiService(new OllamaSettings(), transport);
            ai.Attach(sim);
            for (var i = 0; i < 3; i++)
            {
                ai.Update(now);
                now += 0.1;
            }

            Assert.That(ai.Status, Is.EqualTo(AiConnectionStatus.Connected));
            return ai;
        }

        private void Run(AiService ai, TownSimulation sim, int minutes)
        {
            for (var i = 0; i < minutes; i++)
            {
                ai.Update(now);
                sim.Tick();
                now += 1;
            }

            ai.Update(now);
        }

        private static string Line(string say, string tone = "warm", string topic = "the roses", bool end = false) =>
            "{\"say\":" + FakeAiTransport.Quote(say) + ",\"tone\":\"" + tone + "\",\"topic\":\"" + topic + "\",\"end\":" + (end ? "true" : "false") + "}";

        [Test]
        public void ConnectedAi_WritesTheLines_AndTheGameKeepsTheTurns()
        {
            var sim = TestTown.MabelAndArthurOnTheGreen(TestTown.WeekendAfternoon);
            var transport = FakeAiTransport.WorkingOllama(body => body.Contains(DialogueMarker) ? Line("The roses are glorious this year!") : Relax);
            var ai = Connected(sim, transport);

            Run(ai, sim, SocialRules.MaxConversationMinutes + 5);

            var chat = sim.Conversations.All.Single(c => c.Kind == ConversationKind.Chat);
            Assert.That(chat.Lines.Count, Is.InRange(SocialRules.MinLines, SocialRules.MaxLines));
            Assert.That(chat.Lines.All(l => l.FromAi && l.Text == "The roses are glorious this year!" && l.Tone == ConversationTone.Warm), Is.True);
            Assert.That(chat.Context, Is.EqualTo("the roses"));
            for (var i = 1; i < chat.Lines.Count; i++)
            {
                Assert.That(chat.Lines[i].SpeakerId, Is.Not.EqualTo(chat.Lines[i - 1].SpeakerId));
            }

            var prompts = transport.Calls.Where(c => c.Body != null && c.Body.Contains(DialogueMarker)).ToList();
            Assert.That(prompts.Count, Is.EqualTo(chat.Lines.Count), "one request per line");
            Assert.That(prompts[0].Body, Does.Contain("You start the conversation"));
            Assert.That(prompts[1].Body, Does.Contain("The conversation so far"));
            Assert.That(prompts[1].Body, Does.Contain("The roses are glorious this year!"));
            Assert.That(prompts[0].Body, Does.Contain("\"disagree\""), "tone is limited by the schema");

            var warm = sim.Relationships.Get(StarterTown.ArthurId, StarterTown.ResidentId).Friendship;
            Assert.That(warm, Is.GreaterThan(15), "warm words count for more than small talk");
        }

        [TestCase("not json at all")]
        [TestCase("{\"say\":\"\",\"tone\":\"warm\"}")]
        [TestCase("{\"tone\":\"warm\"}")]
        public void BadAiLines_FallBackToSmallTalk(string reply)
        {
            var sim = TestTown.MabelAndArthurOnTheGreen(TestTown.WeekendAfternoon);
            var transport = FakeAiTransport.WorkingOllama(body => body.Contains(DialogueMarker) ? reply : Relax);
            var ai = Connected(sim, transport);

            Run(ai, sim, SocialRules.MaxConversationMinutes + 5);

            var chat = sim.Conversations.All.Single(c => c.Kind == ConversationKind.Chat);
            Assert.That(chat.Lines.Count, Is.GreaterThanOrEqualTo(SocialRules.MinLines));
            Assert.That(chat.Lines.Any(l => l.FromAi), Is.False);
            Assert.That(ai.Queue.Stats.InvalidReplies, Is.GreaterThan(0));
            Assert.That(ai.Status, Is.EqualTo(AiConnectionStatus.Connected), "a bad answer isn't a lost connection");
        }

        [Test]
        public void SilentOllama_StillLetsResidentsChat()
        {
            var sim = TestTown.MabelAndArthurOnTheGreen(TestTown.WeekendAfternoon);
            var transport = FakeAiTransport.WorkingOllama(body => body.Contains(DialogueMarker) ? null : Relax);
            var ai = Connected(sim, transport);

            Run(ai, sim, SocialRules.MaxConversationMinutes + 5);

            var chat = sim.Conversations.All.Single(c => c.Kind == ConversationKind.Chat);
            Assert.That(chat.Lines.Count, Is.GreaterThanOrEqualTo(SocialRules.MinLines));
            Assert.That(chat.Lines.Any(l => l.FromAi), Is.False);
            Assert.That(sim.ActiveConversations, Is.Empty);
        }

        [Test]
        public void AiCanWrapUpEarly()
        {
            var sim = TestTown.MabelAndArthurOnTheGreen(TestTown.WeekendAfternoon);
            var transport = FakeAiTransport.WorkingOllama(body => body.Contains(DialogueMarker) ? Line("Must dash!", "playful", "errands", end: true) : Relax);
            var ai = Connected(sim, transport);

            Run(ai, sim, SocialRules.MaxConversationMinutes + 5);

            var chat = sim.Conversations.All.Single(c => c.Kind == ConversationKind.Chat);
            Assert.That(chat.Lines.Count, Is.EqualTo(SocialRules.MinLines));
        }

        [Test]
        public void DialoguePrompt_KnowsWhoTheyAreTalkingTo_AndWhatHappenedBefore()
        {
            var sim = TestTown.MabelAndArthurOnTheGreen(TestTown.WeekendAfternoon);
            sim.Advance(SocialRules.MaxConversationMinutes + 5);
            Assert.That(sim.Conversations.All.Any(c => c.Kind == ConversationKind.Chat), Is.True);
            sim.Advance(SocialRules.PairCooldownMinutes);
            sim.AiDecisionsEnabled = true;
            foreach (var r in sim.Residents)
            {
                // Back on the green after the cooldown, and ready for company.
                r.ResetActivity();
                r.Location = r.Id == StarterTown.ResidentId ? new Location(StarterTown.VillageMapId, 10, 9) : new Location(StarterTown.VillageMapId, 11, 9);
                r.Needs[NeedType.Social] = 40;
            }

            ConversationSession session = null;
            for (var i = 0; i < 30 && (session == null || !session.AwaitingLine); i++)
            {
                sim.Tick();
                session = sim.ActiveConversations.FirstOrDefault();
            }

            Assert.That(session, Is.Not.Null);
            var speaker = sim.GetResident(session.SpeakerId);
            var listener = sim.GetResident(session.ListenerId);
            var memories = sim.Memories.Recall(speaker.Id, sim.Time, new[] { listener.Id, "chat", "social" }, DialoguePrompt.MemoryCount);
            var text = DialoguePrompt.UserText(sim, session, memories);

            Assert.That(text, Does.Contain($"You are chatting with {listener.FullName}"));
            Assert.That(text, Does.Contain("acquaintance").Or.Contain("barely knows").Or.Contain("friend"));
            Assert.That(text, Does.Contain("Last time you talked"));
            Assert.That(text, Does.Contain("for the first time"), "the first meeting is recalled");
            Assert.That(DialoguePrompt.BuildBody(FakeAiTransport.Model, sim, session), Does.Contain(speaker.FullName));
        }

        [TestCase("{\"say\":\"Hi\",\"tone\":\"warm\",\"topic\":\"x\",\"end\":\"true\"}", true)]
        [TestCase("{\"say\":\"Hi\",\"tone\":\"warm\",\"topic\":\"x\",\"end\":true}", true)]
        [TestCase("{\"say\":\"Hi\"}", false)]
        public void DialogueReply_IsReadLeniently(string content, bool end)
        {
            Assert.That(DialoguePrompt.TryParse(content, out var reply, out var error), Is.True, error);
            Assert.That(reply.Say, Is.EqualTo("Hi"));
            Assert.That(reply.End, Is.EqualTo(end));
        }

        [Test]
        public void DecisionPrompt_OffersChats_WithTheResidentId()
        {
            var sim = TestTown.MabelAndArthurOnTheGreen(TestTown.WeekendAfternoon, social: 90);
            var mabel = sim.GetResident(StarterTown.ResidentId);
            var options = sim.FreeTimeOptionsFor(mabel);
            var chat = options.Single(o => o.Partner != null);

            var text = DecisionPrompt.UserText(sim, mabel, options, new MemoryRecord[0]);

            Assert.That(chat.TargetId, Is.EqualTo(StarterTown.ArthurId));
            Assert.That(text, Does.Contain($"target_id \"{StarterTown.ArthurId}\": go and chat with Arthur Bramble"));
            Assert.That(text, Does.Contain("People in the village:"));
            Assert.That(text, Does.Contain("Arthur Bramble, 71: you have never really met"));
            Assert.That(DecisionPrompt.Cues(sim, mabel, options), Does.Contain(StarterTown.ArthurId));
            Assert.That(sim.ApplyDecision(mabel.Id, 1, chat.ActionId, StarterTown.NellId, "x", null), Is.Not.EqualTo(DecisionOutcome.Applied));
        }
    }
}
