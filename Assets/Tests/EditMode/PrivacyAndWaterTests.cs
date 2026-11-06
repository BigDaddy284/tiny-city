using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation;
using TinyCity.Simulation.AI;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Social;

namespace TinyCity.Tests.EditMode
{
    /// <summary>Owner, 6 November: personalities stay private, and nobody is told the dirty pond is healthy.</summary>
    public sealed class PrivacyAndWaterTests
    {
        [Test]
        public void Traits_AreTheFirstSentencesWords()
        {
            Assert.That(PrivateTraits.TraitsOf("Nosy, chatty and opinionated. Knows everyone's business."), Is.EqualTo(new[] { "nosy", "chatty", "opinionated" }));
            Assert.That(PrivateTraits.TraitsOf("Proud, warm and a bit of a show-off. Loves feeding people."), Is.EqualTo(new[] { "proud", "warm", "show-off" }));
            Assert.That(PrivateTraits.TraitsOf(""), Is.Empty);
        }

        [Test]
        public void TraitTalk_IsLeftOutOfWhatTheySay()
        {
            var sim = FlatLand.CreateNewGame();
            var people = sim.Residents;
            Assert.That(PrivateTraits.WithoutTraitTalk("I'm nosy, I know. Want some fish?", people), Is.EqualTo("Want some fish?"));
            Assert.That(PrivateTraits.WithoutTraitTalk("You're so tight-fisted! Give me wood.", people), Is.EqualTo("Give me wood."));
            Assert.That(PrivateTraits.WithoutTraitTalk("My personality just clashes with yours.", people), Is.Empty);
            Assert.That(PrivateTraits.WithoutTraitTalk("The fish are biting today. Shall we go?", people), Is.EqualTo("The fish are biting today. Shall we go?"));
        }

        [Test]
        public void ALineThatIsAllTraitTalk_IsAskedForAgain()
        {
            var sim = TestTown.TwoSideBySide(TestTown.WeekendAfternoon, 10);
            sim.AiDecisionsEnabled = true;
            var session = TestTown.StartChat(sim);
            for (var i = 0; i < 30 && !session.AwaitingLine; i++)
            {
                sim.Tick();
            }

            var outcome = sim.ApplyConversationLine(session.Id, session.LineRequestId, session.SpeakerId, "I'm so nosy.", "friendly", "chat", false);
            Assert.That(outcome, Is.EqualTo(DecisionOutcome.InvalidText));
        }

        [Test]
        public void EveryPrompt_SaysThePondIsNeverHealthy_AndPersonalitiesArePrivate()
        {
            var sim = FlatLand.CreateNewGame();
            var r = sim.Residents[0];
            Assert.That(PromptText.Persona(r, sim), Does.Contain("Never talk about personalities"));
            var decision = DecisionPrompt.UserText(sim, r, sim.FreeTimeOptionsFor(r), new TinyCity.Simulation.Memory.MemoryRecord[0]);
            Assert.That(decision, Does.Contain(PromptText.DirtyWater));
            Assert.That(decision, Does.Not.Contain("drink-pond").And.Not.Contain("eat-raw"), "pond water can't be drunk, nor raw food eaten");
            Assert.That(PromptText.DirtyWater, Does.Contain("nobody can drink it"));
        }
    }
}
