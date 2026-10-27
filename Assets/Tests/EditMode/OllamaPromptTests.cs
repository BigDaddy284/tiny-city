using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyTown.Simulation.AI;
using TinyTown.Simulation.Persistence;
using TinyTown.Simulation.Residents;

namespace TinyTown.Tests.EditMode
{
    public class OllamaPromptTests
    {
        [Test]
        public void Urls_AreBuiltFromTheConfiguredEndpoint()
        {
            Assert.That(OllamaProtocol.ChatUrl("http://localhost:11434/"), Is.EqualTo("http://localhost:11434/api/chat"));
            Assert.That(OllamaProtocol.TagsUrl(" http://pc:9999 "), Is.EqualTo("http://pc:9999/api/tags"));
        }

        [Test]
        public void ChatContent_IsRead_AndOllamaErrorsAreReported()
        {
            Assert.That(OllamaProtocol.TryReadChatContent(FakeAiTransport.ChatReply("{\"a\":1}"), out var content, out _), Is.True);
            Assert.That(content, Is.EqualTo("{\"a\":1}"));

            Assert.That(OllamaProtocol.TryReadChatContent("{\"error\":\"model 'x' not found\"}", out _, out var error), Is.False);
            Assert.That(error, Does.Contain("model 'x' not found"));

            Assert.That(OllamaProtocol.TryReadChatContent("<html>proxy error</html>", out _, out error), Is.False);
            Assert.That(OllamaProtocol.TryReadChatContent("{\"done\":true}", out _, out error), Is.False);
        }

        [Test]
        public void InstalledModels_MatchWithOrWithoutTheLatestTag()
        {
            Assert.That(OllamaProtocol.TryReadModelNames("{\"models\":[{\"name\":\"qwen2.5:7b\"},{\"name\":\"llama3:latest\"}]}", out var names, out _), Is.True);

            Assert.That(OllamaProtocol.HasModel(names, "qwen2.5:7b"), Is.True);
            Assert.That(OllamaProtocol.HasModel(names, "Qwen2.5:7B"), Is.True);
            Assert.That(OllamaProtocol.HasModel(names, "llama3"), Is.True);
            Assert.That(OllamaProtocol.HasModel(names, "qwen2.5"), Is.False);
            Assert.That(OllamaProtocol.TryReadModelNames("{}", out _, out _), Is.False);
        }

        [Test]
        public void DecisionRequest_OffersOnlyValidOptions_AndConstrainsTheAnswer()
        {
            var sim = TestTown.WithResident(TestTown.WeekendAfternoon, adjust: n => n[NeedType.Fun] = 30);
            var r = sim.Residents[0];
            sim.Memories.Add(r.Id, sim.Time, TinyTown.Simulation.Memory.MemoryType.Event, 5, "My armchair has a wobbly leg.", tags: new[] { "obj-cottage1-armchair" });
            var options = sim.Planner.FreeTimeOptions(r);

            var body = (Dictionary<string, object>)Json.Parse(DecisionPrompt.BuildBody("qwen2.5:7b", sim, r, options));
            var messages = (List<object>)body["messages"];
            var user = (string)((Dictionary<string, object>)messages[1])["content"];
            var schema = (Dictionary<string, object>)body["format"];
            var actions = (List<object>)((Dictionary<string, object>)((Dictionary<string, object>)schema["properties"])["action"])["enum"];

            Assert.That(body["model"], Is.EqualTo("qwen2.5:7b"));
            Assert.That(body["stream"], Is.EqualTo(false));
            Assert.That(user, Does.Contain("wobbly leg"));
            Assert.That(user, Does.Contain("Saturday 14:00"));
            Assert.That(actions.Contains("read"), Is.True);
            Assert.That(actions.Contains("relax"), Is.True);
            Assert.That(actions.Contains("work"), Is.False);
            Assert.That(actions.Contains("sleep"), Is.False);
            Assert.That(options.All(o => user.Contains(o.TargetId)), Is.True);
        }

        [Test]
        public void DecisionReply_FillsInTheOnlyPossibleTarget()
        {
            var sim = TestTown.WithResident(TestTown.WeekendAfternoon, adjust: n => n[NeedType.Fun] = 30);
            var options = sim.Planner.FreeTimeOptions(sim.Residents[0]);

            Assert.That(DecisionPrompt.TryParse("{\"action\":\"read\"}", options, out var reply, out _), Is.True);
            Assert.That(reply.TargetId, Is.EqualTo("obj-cottage1-armchair"));
            Assert.That(DecisionPrompt.TryParse("{\"reason\":\"no action\"}", options, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("no action"));
        }

        [TestCase("{\"summary\":\"Lovely day\",\"importance\":3}", 3)]
        [TestCase("{\"summary\":\"Lovely day\",\"importance\":\"4\"}", 4)]
        [TestCase("{\"summary\":\"Lovely day\"}", 2)]
        public void ReflectionReply_IsRead(string content, int importance)
        {
            Assert.That(ReflectionPrompt.TryParse(content, out var reply, out _), Is.True);
            Assert.That(reply.Summary, Is.EqualTo("Lovely day"));
            Assert.That(reply.Importance, Is.EqualTo(importance));
        }

        [TestCase("")]
        [TestCase("no json here")]
        [TestCase("{\"summary\":\"\"}")]
        [TestCase("[1,2,3]")]
        public void ReflectionReply_RejectsUnusableAnswers(string content)
        {
            Assert.That(ReflectionPrompt.TryParse(content, out _, out var error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void AiJson_RejectsHugeAnswers()
        {
            Assert.That(AiJson.TryParseObject("{\"a\":\"" + new string('x', AiJson.MaxLength) + "\"}", out _, out var error), Is.False);
            Assert.That(error, Does.Contain("too long"));
        }
    }
}
