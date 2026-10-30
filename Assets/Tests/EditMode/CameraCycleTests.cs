using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation.Configuration;
using TinyCity.Simulation.Content;

namespace TinyCity.Tests.EditMode
{
    /// <summary>The camera cycle: when it turns, what it shows, and how the open panels share the list.</summary>
    public class CameraCycleTests
    {
        private static List<CameraTarget> People(int count) => Enumerable.Range(1, count).Select(i => CameraTarget.Resident($"res-{i}")).ToList();

        private static List<CameraTarget> Buildings(int count) => Enumerable.Range(1, count).Select(i => CameraTarget.Place($"map-{i}")).ToList();

        private static List<string> Names(CameraTarget?[] targets) => targets.Select(t => t.HasValue ? t.Value.ToString() : "-").ToList();

        [Test]
        public void OnePanel_ShowsEachResidentInTurn_AndWrapsRound()
        {
            var shown = Enumerable.Range(0, 4).Select(turn => Names(CameraCycle.Assign(CycleMode.Residents, People(3), Buildings(2), 1, turn))[0]).ToList();
            Assert.That(shown, Is.EqualTo(new List<string> { "res:res-1", "res:res-2", "res:res-3", "res:res-1" }));
        }

        [Test]
        public void FourPanels_ShareTheList_FourAtATime()
        {
            Assert.That(Names(CameraCycle.Assign(CycleMode.Residents, People(8), Buildings(0), 4, 0)), Is.EqualTo(new List<string> { "res:res-1", "res:res-2", "res:res-3", "res:res-4" }));
            Assert.That(Names(CameraCycle.Assign(CycleMode.Residents, People(8), Buildings(0), 4, 1)), Is.EqualTo(new List<string> { "res:res-5", "res:res-6", "res:res-7", "res:res-8" }));
            Assert.That(Names(CameraCycle.Assign(CycleMode.Residents, People(8), Buildings(0), 4, 2)), Is.EqualTo(new List<string> { "res:res-1", "res:res-2", "res:res-3", "res:res-4" }));
        }

        [Test]
        public void MorePanelsThanThings_EachShownOnce_TheRestKeepTheirs()
        {
            Assert.That(Names(CameraCycle.Assign(CycleMode.Buildings, People(5), Buildings(2), 4, 3)), Is.EqualTo(new List<string> { "map:map-1", "map:map-2", "-", "-" }));
        }

        [Test]
        public void HalfAndHalf_SplitsThePanels_AndOnePanelAlternates()
        {
            Assert.That(Names(CameraCycle.Assign(CycleMode.HalfAndHalf, People(6), Buildings(6), 4, 1)), Is.EqualTo(new List<string> { "res:res-3", "res:res-4", "map:map-3", "map:map-4" }));
            var one = Enumerable.Range(0, 4).Select(turn => Names(CameraCycle.Assign(CycleMode.HalfAndHalf, People(3), Buildings(3), 1, turn))[0]).ToList();
            Assert.That(one, Is.EqualTo(new List<string> { "res:res-1", "map:map-1", "res:res-2", "map:map-2" }));
        }

        [Test]
        public void CycleAll_GoesThroughEveryoneThenEveryBuilding()
        {
            var shown = Enumerable.Range(0, 5).Select(turn => Names(CameraCycle.Assign(CycleMode.Everything, People(2), Buildings(3), 1, turn))[0]).ToList();
            Assert.That(shown, Is.EqualTo(new List<string> { "res:res-1", "res:res-2", "map:map-1", "map:map-2", "map:map-3" }));
        }

        [Test]
        public void ATurn_IsDueOnceTheIntervalHasPassed()
        {
            var cycle = new CameraCycle();
            Assert.That(cycle.Due(600, 60), Is.True, "the first ask starts the cycle");
            Assert.That(cycle.Turn, Is.EqualTo(0));
            Assert.That(cycle.Due(659, 60), Is.False);
            Assert.That(cycle.MinutesLeft(659, 60), Is.EqualTo(1));
            Assert.That(cycle.Due(660, 60), Is.True);
            Assert.That(cycle.Turn, Is.EqualTo(1));
            cycle.Skip(670);
            Assert.That(cycle.Turn, Is.EqualTo(2));
            Assert.That(cycle.Due(729, 60), Is.False, "Next Now restarts the wait");
            cycle.Restart(800);
            Assert.That(cycle.Turn, Is.EqualTo(0));
            Assert.That(cycle.Due(500, 60), Is.True, "a clock that went backwards starts again");
        }

        [Test]
        public void TheCity_OffersItsLivingResidents_AndBuildingsThatAreUsed()
        {
            var town = StarterTown.CreateNewGame();
            var (residents, buildings) = CameraCycle.Targets(town, town.World.Maps, busyOnly: false);
            Assert.That(residents.Count, Is.EqualTo(town.Residents.Count(r => r.IsAlive)));
            var ids = buildings.Select(b => b.MapId).ToList();
            Assert.That(ids.Contains("map-bar"), Is.True, "businesses are buildings");
            Assert.That(ids.Contains("map-flat-b1"), Is.False, "empty homes are left out");
            Assert.That(ids.Contains(town.Residents[0].HomeMapId), Is.True, "homes people live in are buildings");
            Assert.That(ids.Any(id => town.World.GetMap(id).IsExterior), Is.False);

            var (_, busy) = CameraCycle.Targets(town, town.World.Maps, busyOnly: true);
            Assert.That(busy.All(b => town.Residents.Any(r => r.IsAlive && r.Location.MapId == b.MapId)), Is.True, "only buildings with someone in");
        }

        [Test]
        public void CycleChoices_RoundTrip_AndBadOnesFallBack()
        {
            var prefs = new PlayerPreferences { CycleCameras = true, CycleMinutes = 180, CycleMode = CycleMode.HalfAndHalf, CycleBusyOnly = false };
            var back = PlayerPreferences.FromJson(prefs.ToJson(), out var problems);
            Assert.That(problems, Is.Empty);
            Assert.That(back.CycleCameras, Is.True);
            Assert.That(back.CycleMinutes, Is.EqualTo(180));
            Assert.That(back.CycleMode, Is.EqualTo(CycleMode.HalfAndHalf));
            Assert.That(back.CycleBusyOnly, Is.False);

            var bad = PlayerPreferences.FromJson("{\"cycleMinutes\": 45, \"cycleMode\": \"Sideways\"}", out problems);
            Assert.That(bad.CycleMinutes, Is.EqualTo(CameraCycle.DefaultInterval));
            Assert.That(bad.CycleMode, Is.EqualTo(CycleMode.Everything));
            Assert.That(bad.CycleCameras, Is.False, "off unless chosen");
            Assert.That(problems.Count, Is.EqualTo(2));
        }
    }
}
