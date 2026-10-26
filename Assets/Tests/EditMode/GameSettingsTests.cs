using NUnit.Framework;
using TinyTown.Simulation.Configuration;

namespace TinyTown.Tests.EditMode
{
    // Plain NUnit with no UnityEngine usage, so these also run under `dotnet test` (see DotNetTests/).
    public class GameSettingsTests
    {
        [Test]
        public void Defaults_AreValid_AndPointAtLocalQwen()
        {
            var settings = new GameSettings();

            Assert.That(settings.Validate(), Is.Empty);
            Assert.That(settings.ollama.endpoint, Is.EqualTo("http://localhost:11434"));
            Assert.That(settings.ollama.model, Is.EqualTo("qwen2.5:7b"));
        }

        [TestCase("")]
        [TestCase("localhost:11434")]
        [TestCase("ftp://localhost:11434")]
        [TestCase(null)]
        public void InvalidEndpoint_IsRejected(string endpoint)
        {
            var settings = new OllamaSettings { endpoint = endpoint };

            Assert.That(settings.Validate(), Has.Count.EqualTo(1));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void EmptyModel_IsRejected(string model)
        {
            var settings = new OllamaSettings { model = model };

            Assert.That(settings.Validate(), Has.Count.EqualTo(1));
        }

        [TestCase(0)]
        [TestCase(-5)]
        [TestCase(OllamaSettings.MaxTimeoutSeconds + 1)]
        public void OutOfRangeTimeout_IsRejected(int timeout)
        {
            var settings = new OllamaSettings { timeoutSeconds = timeout };

            Assert.That(settings.Validate(), Has.Count.EqualTo(1));
        }

        [Test]
        public void CustomValidSettings_AreKept()
        {
            var custom = new GameSettings
            {
                ollama = new OllamaSettings { endpoint = "http://192.168.1.20:11434", model = "llama3.1:8b", timeoutSeconds = 120 },
            };

            var result = GameSettings.ValidOrDefault(custom, out var problems);

            Assert.That(result, Is.SameAs(custom));
            Assert.That(problems, Is.Empty);
        }

        [Test]
        public void InvalidSettings_FallBackToDefaults_WithProblemsReported()
        {
            var broken = new GameSettings { ollama = new OllamaSettings { model = "" } };

            var result = GameSettings.ValidOrDefault(broken, out var problems);

            Assert.That(result.Validate(), Is.Empty);
            Assert.That(result.ollama.model, Is.EqualTo(OllamaSettings.DefaultModel));
            Assert.That(problems, Is.Not.Empty);
        }

        [Test]
        public void MissingSettings_FallBackToDefaults()
        {
            var result = GameSettings.ValidOrDefault(null, out var problems);

            Assert.That(result.Validate(), Is.Empty);
            Assert.That(problems, Has.Count.EqualTo(1));
        }

        [TestCase(0)]
        [TestCase(GameSettings.CurrentVersion + 1)]
        public void UnsupportedVersion_IsRejected(int version)
        {
            var settings = new GameSettings { version = version };

            Assert.That(settings.Validate(), Has.Count.EqualTo(1));
        }

        [Test]
        public void MissingOllamaSection_IsRejected()
        {
            var settings = new GameSettings { ollama = null };

            Assert.That(settings.Validate(), Has.Count.EqualTo(1));
        }
    }
}
