using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TinyCity.Runtime;
using TinyCity.Simulation.History;
using UnityEngine;
using UnityEngine.TestTools;

namespace TinyCity.Tests.PlayMode
{
    /// <summary>The Phase 5 journal, notifications and exports in the running game.</summary>
    public class JournalPlayTests
    {
        private GameController game;

        [UnitySetUp]
        public IEnumerator FindGame()
        {
            yield return null;
            yield return null;
            game = Object.FindAnyObjectByType<GameController>();
            Assert.That(game, Is.Not.Null, "GameController was not created.");
        }

        [TearDown]
        public void CloseJournal()
        {
            if (game != null && game.JournalOpen)
            {
                game.ToggleJournal();
            }
        }

        [UnityTest]
        public IEnumerator NotableEvent_PopsUp_ButEverydayOnesDoNot()
        {
            var r = game.Simulation.Residents.First(x => x.IsAlive);
            var birthday = game.Simulation.History.Add(game.Simulation.Time, TownEventKind.Birthday, $"{r.FullName} had a test birthday.", new[] { r.Id });
            var couple = game.Simulation.History.Add(game.Simulation.Time, TownEventKind.Couple, $"{r.FullName} found a test sweetheart.", new[] { r.Id });
            yield return null;

            var showing = game.Notifications.Showing.ToList();
            Assert.That(showing.Contains(couple), Is.True, "a new couple is a notification");
            Assert.That(showing.Contains(birthday), Is.False, "a birthday is only in the journal");
        }

        [UnityTest]
        public IEnumerator Journal_OpensAndCloses()
        {
            Assert.That(game.JournalOpen, Is.False);
            game.ToggleJournal();
            yield return null;
            Assert.That(game.JournalOpen, Is.True);
            game.ToggleJournal();
            yield return null;
            Assert.That(game.JournalOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator Exports_WriteTextFiles()
        {
            game.ExportTownHistory();
            var historyFile = Path.Combine(ExportStore.Folder, $"Town history - day {game.Simulation.Time.Day}.txt");
            Assert.That(File.Exists(historyFile), Is.True, game.Status);
            Assert.That(File.ReadAllText(historyFile), Does.StartWith("A history of Tiny City"));
            File.Delete(historyFile);

            var r = game.Simulation.Residents[0];
            game.ExportLifeStory(r);
            var storyFile = Path.Combine(ExportStore.Folder, $"{ExportStore.SafeFileName($"{r.FullName} life story")} - day {game.Simulation.Time.Day}.txt");
            Assert.That(File.Exists(storyFile), Is.True, game.Status);
            Assert.That(File.ReadAllText(storyFile), Does.Contain($"The life of {r.FullName}"));
            File.Delete(storyFile);
            yield return null;
        }

        [Test]
        public void FileNames_AreSafe()
        {
            Assert.That(ExportStore.SafeFileName("Mia O'Hara: life/story?"), Is.EqualTo("Mia O'Hara lifestory"));
            Assert.That(ExportStore.SafeFileName("***"), Is.EqualTo("export"));
        }
    }
}
