using System.Collections;
using System.IO;
using NUnit.Framework;
using TinyCity.Runtime;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Persistence;
using UnityEngine;
using UnityEngine.TestTools;

namespace TinyCity.Tests.PlayMode
{
    /// <summary>Phase 5 save slots in the running game, on real files.</summary>
    public class SaveSlotPlayTests
    {
        [UnityTest]
        public IEnumerator Towns_CanBeStartedCopiedRenamedExportedSwitchedAndDeleted()
        {
            yield return null;
            yield return null;
            var game = Object.FindAnyObjectByType<GameController>();
            Assert.That(game, Is.Not.Null);
            game.Save("test");
            var original = game.CurrentSlot;
            var originalMinutes = game.Simulation.Time.TotalMinutes;
            Assert.That(File.Exists(SaveStore.PathFor(original)), Is.True, game.Status);

            var fresh = game.NewTownSlot("Test town");
            Assert.That(fresh, Is.Not.EqualTo(original));
            Assert.That(game.CurrentSlot, Is.EqualTo(fresh));
            Assert.That(game.CurrentTownName, Is.EqualTo("Test town"));
            Assert.That(game.Simulation.Time.TotalMinutes, Is.EqualTo(StarterTown.StartTime.TotalMinutes), "a new town starts at the beginning");
            Assert.That(File.Exists(SaveStore.PathFor(fresh)), Is.True, game.Status);
            Assert.That(SaveStore.ListSlots(), Does.Contain(original).And.Contain(fresh));

            game.AdvanceMinutes(30);
            var copy = game.DuplicateSlot(fresh);
            Assert.That(copy, Is.Not.Null, game.Status);
            Assert.That(game.CurrentSlot, Is.EqualTo(fresh), "copying doesn't switch towns");
            Assert.That(game.SlotNames.NameOf(copy), Is.EqualTo("Copy of Test town"));
            Assert.That(File.ReadAllText(SaveStore.PathFor(copy)), Is.EqualTo(File.ReadAllText(SaveStore.PathFor(fresh))), "the copy is exact and up to date");

            game.RenameSlot(copy, "Test town, the sequel");
            Assert.That(SaveStore.ReadIndex().NameOf(copy), Is.EqualTo("Test town, the sequel"), "names are written to disk");

            game.ExportSlot(copy);
            var exported = Path.Combine(ExportStore.Folder, $"{ExportStore.SafeFileName("Test town, the sequel save")} - day 1.json");
            Assert.That(File.Exists(exported), Is.True, game.Status);
            Assert.That(SaveSummary.Read(File.ReadAllText(exported)).IsReadable, Is.True);
            File.Delete(exported);

            Assert.That(game.DeleteSlot(fresh), Is.False, "the town being played can't be deleted");
            Assert.That(game.LoadSlot(copy), Is.True, game.Status);
            Assert.That(game.CurrentSlot, Is.EqualTo(copy));
            Assert.That(game.Simulation.Time.TotalMinutes, Is.EqualTo(StarterTown.StartTime.TotalMinutes + 30));

            Assert.That(game.LoadSlot(original), Is.True, game.Status);
            Assert.That(game.CurrentSlot, Is.EqualTo(original));
            Assert.That(game.Simulation.Time.TotalMinutes, Is.EqualTo(originalMinutes), "back where we left the first town");

            Assert.That(game.DeleteSlot(fresh), Is.True, game.Status);
            Assert.That(game.DeleteSlot(copy), Is.True, game.Status);
            Assert.That(File.Exists(SaveStore.PathFor(fresh)) || File.Exists(SaveStore.PathFor(copy)), Is.False);
            Assert.That(SaveStore.ListSlots(), Does.Not.Contain(fresh).And.Not.Contain(copy));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SavesWindow_OpensAndCloses_AndClosesTheJournal()
        {
            yield return null;
            var game = Object.FindAnyObjectByType<GameController>();
            game.ToggleJournal();
            game.ToggleSaves();
            yield return null;
            Assert.That(game.SavesOpen, Is.True);
            Assert.That(game.JournalOpen, Is.False, "one window at a time");
            game.ToggleSaves();
            Assert.That(game.SavesOpen, Is.False);
        }
    }
}
