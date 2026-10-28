using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyTown.Simulation.Configuration;

namespace TinyTown.Tests.EditMode
{
    /// <summary>The Settings window's choices: saved and read back exactly, and damaged files never break the game.</summary>
    public class PlayerPreferencesTests
    {
        [Test]
        public void RoundTrip_KeepsEveryChoice()
        {
            var prefs = new PlayerPreferences
            {
                AiEnabled = false,
                AiModel = "llama3:latest",
                MasterVolume = 55,
                MusicVolume = 10,
                AmbienceVolume = 0,
                EffectsVolume = 100,
                Window = WindowChoice.Borderless,
                Width = 1920,
                Height = 1080,
                VSync = false,
                FrameLimit = 144,
                Theme = ThemeChoice.Light,
            };

            var loaded = PlayerPreferences.FromJson(prefs.ToJson(), out var problems);

            Assert.That(problems, Is.Empty);
            Assert.That(loaded.ToJson(), Is.EqualTo(prefs.ToJson()));
            Assert.That(loaded.AiEnabled, Is.EqualTo(false));
            Assert.That(loaded.AiModel, Is.EqualTo("llama3:latest"));
            Assert.That(loaded.Window, Is.EqualTo(WindowChoice.Borderless));
            Assert.That(loaded.Theme, Is.EqualTo(ThemeChoice.Light));
            Assert.That(loaded.FrameLimit, Is.EqualTo(144));
        }

        [Test]
        public void Defaults_LeaveSettingsJsonInCharge()
        {
            var prefs = PlayerPreferences.FromJson(new PlayerPreferences().ToJson(), out var problems);
            var ollama = new OllamaSettings { enabled = true, model = "qwen2.5:7b" };

            prefs.ApplyTo(ollama);

            Assert.That(problems, Is.Empty);
            Assert.That(prefs.AiEnabled, Is.Null);
            Assert.That(ollama.enabled, Is.True);
            Assert.That(ollama.model, Is.EqualTo("qwen2.5:7b"));
            Assert.That(prefs.Theme, Is.EqualTo(ThemeChoice.Dark));
        }

        [Test]
        public void ApplyTo_OverridesAiChoices()
        {
            var ollama = new OllamaSettings { enabled = true, model = "qwen2.5:7b" };

            new PlayerPreferences { AiEnabled = false, AiModel = "mistral:7b" }.ApplyTo(ollama);

            Assert.That(ollama.enabled, Is.False);
            Assert.That(ollama.model, Is.EqualTo("mistral:7b"));
        }

        [TestCase("")]
        [TestCase("not json")]
        [TestCase("[1, 2]")]
        [TestCase("{\"theme\":")]
        public void BadFile_GivesDefaults(string json)
        {
            var prefs = PlayerPreferences.FromJson(json, out _);

            Assert.That(prefs.ToJson(), Is.EqualTo(new PlayerPreferences().ToJson()));
        }

        [Test]
        public void OutOfRangeValues_AreKeptInRange_AndReported()
        {
            var json = "{\"masterVolume\": 250, \"musicVolume\": -5, \"frameLimit\": 9999, \"width\": 300, \"height\": 200,"
                + " \"theme\": \"Auto\", \"window\": 7, \"aiModel\": \"   \", \"vsync\": \"yes\"}";

            var prefs = PlayerPreferences.FromJson(json, out var problems);

            Assert.That(prefs.MasterVolume, Is.EqualTo(PlayerPreferences.MaxVolume));
            Assert.That(prefs.MusicVolume, Is.EqualTo(0));
            Assert.That(prefs.FrameLimit, Is.EqualTo(PlayerPreferences.MaxFrameLimit));
            Assert.That(prefs.Width, Is.EqualTo(0));
            Assert.That(prefs.Height, Is.EqualTo(0));
            Assert.That(prefs.Theme, Is.EqualTo(ThemeChoice.Dark));
            Assert.That(prefs.Window, Is.EqualTo(WindowChoice.Windowed));
            Assert.That(prefs.AiModel, Is.Null);
            Assert.That(prefs.VSync, Is.True);
            Assert.That(problems.Count, Is.GreaterThanOrEqualTo(6));
        }

        [Test]
        public void Volume_IsMasterTimesChannel()
        {
            var prefs = new PlayerPreferences { MasterVolume = 50, MusicVolume = 50 };

            Assert.That(prefs.Volume(prefs.MusicVolume), Is.EqualTo(0.25).Within(1e-9));
            Assert.That(new PlayerPreferences { MasterVolume = 0 }.Volume(100), Is.EqualTo(0));
        }
    }
}
