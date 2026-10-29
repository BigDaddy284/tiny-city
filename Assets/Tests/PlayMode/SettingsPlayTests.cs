using System.Collections;
using System.IO;
using NUnit.Framework;
using TinyTown.Runtime;
using TinyTown.Simulation.Configuration;
using UnityEngine;
using UnityEngine.TestTools;

namespace TinyTown.Tests.PlayMode
{
    /// <summary>The Settings window in the running game: opening it, the theme, and the settings file.</summary>
    public class SettingsPlayTests
    {
        [UnityTest]
        public IEnumerator SettingsWindow_OpensAndCloses_AndClosesOtherWindows()
        {
            yield return null;
            var game = Object.FindAnyObjectByType<GameController>();
            game.ToggleSaves();
            game.ToggleSettings();
            yield return null;
            Assert.That(game.SettingsOpen, Is.True);
            Assert.That(game.SavesOpen, Is.False, "one window at a time");
            game.ToggleJournal();
            Assert.That(game.SettingsOpen, Is.False);
            game.ToggleJournal();
            Assert.That(game.JournalOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator Theme_SwitchesTheCamerasAndSkin()
        {
            yield return null;
            var game = Object.FindAnyObjectByType<GameController>();
            var before = UiTheme.Choice;
            try
            {
                UiTheme.Choose(ThemeChoice.Light);
                yield return null;
                Assert.That(Camera.main.backgroundColor, Is.EqualTo(UiTheme.ToColor(UiPalette.LightPalette.Canvas)));
                UiTheme.Choose(ThemeChoice.Dark);
                yield return null;
                Assert.That(Camera.main.backgroundColor, Is.EqualTo(UiTheme.ToColor(UiPalette.DarkPalette.Canvas)));
                Assert.That(game.Panels.All[0].Camera, Is.SameAs(Camera.main));
            }
            finally
            {
                UiTheme.Choose(before);
            }
        }

        [Test]
        public void PlayerSettings_AreWrittenAndReadBack()
        {
            var keep = File.Exists(PlayerSettingsStore.FilePath) ? File.ReadAllText(PlayerSettingsStore.FilePath) : null;
            try
            {
                var prefs = new PlayerPreferences { Theme = ThemeChoice.Light, MusicVolume = 12, AiEnabled = false };
                Assert.That(PlayerSettingsStore.Save(prefs), Is.Null);
                var loaded = PlayerSettingsStore.Load(out var problems);
                Assert.That(problems, Is.Empty);
                Assert.That(loaded.ToJson(), Is.EqualTo(prefs.ToJson()));
            }
            finally
            {
                if (keep != null)
                {
                    File.WriteAllText(PlayerSettingsStore.FilePath, keep);
                }
                else
                {
                    File.Delete(PlayerSettingsStore.FilePath);
                }
            }
        }
    }
}
