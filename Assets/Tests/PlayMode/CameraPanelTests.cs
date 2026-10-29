using System.Collections;
using System.Linq;
using NUnit.Framework;
using TinyCity.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace TinyCity.Tests.PlayMode
{
    /// <summary>The Phase 5 observer view: one to ten camera panels watching the same town.</summary>
    public class CameraPanelTests
    {
        private GameController game;

        [UnitySetUp]
        public IEnumerator FindGame()
        {
            yield return null;
            yield return null;
            game = Object.FindAnyObjectByType<GameController>();
            Assert.That(game, Is.Not.Null, "GameController was not created.");
            Assert.That(game.Panels, Is.Not.Null);
        }

        [TearDown]
        public void BackToOnePanel()
        {
            game?.SetPanelCount(1);
        }

        [Test]
        public void Layout_FitsEveryPanelInTheArea_WithoutOverlap()
        {
            var area = new Rect(0f, 0f, 900f, 700f);
            for (var count = 1; count <= CameraPanels.MaxPanels; count++)
            {
                var cells = CameraPanels.Layout(count, area);
                Assert.That(cells.Length, Is.EqualTo(count));
                for (var i = 0; i < count; i++)
                {
                    var c = cells[i];
                    Assert.That(c.width > 0f && c.height > 0f, Is.True, $"{count} panels: panel {i} is empty");
                    Assert.That(c.xMin >= area.xMin - 0.01f && c.yMin >= area.yMin - 0.01f && c.xMax <= area.xMax + 0.01f && c.yMax <= area.yMax + 0.01f, Is.True, $"{count} panels: panel {i} {c} is outside {area}");
                    for (var j = i + 1; j < count; j++)
                    {
                        var overlap = Rect.MinMaxRect(Mathf.Max(c.xMin, cells[j].xMin), Mathf.Max(c.yMin, cells[j].yMin), Mathf.Min(c.xMax, cells[j].xMax), Mathf.Min(c.yMax, cells[j].yMax));
                        Assert.That(overlap.width <= 0.5f || overlap.height <= 0.5f, Is.True, $"{count} panels: {i} and {j} overlap");
                    }
                }

                var covered = cells.Sum(c => c.width * c.height);
                Assert.That(covered, Is.EqualTo(area.width * area.height).Within(1f), $"{count} panels should fill the area");
            }
        }

        [UnityTest]
        public IEnumerator Panels_GoUpToTen_AndDownToOne_EachWithItsOwnCamera()
        {
            game.SetPanelCount(CameraPanels.MaxPanels + 3);
            yield return null;
            Assert.That(game.Panels.Count, Is.EqualTo(CameraPanels.MaxPanels));
            Assert.That(game.Panels.All.Select(p => p.Camera).Distinct().Count(), Is.EqualTo(CameraPanels.MaxPanels), "every panel has its own camera");
            Assert.That(game.Panels.All[0].Camera, Is.SameAs(Camera.main), "the first panel uses the main camera");
            Assert.That(game.Panels.All.All(p => p.Camera.enabled), Is.True);

            var living = game.Simulation.Residents.Count(r => r.IsAlive);
            var followed = game.Panels.All.Where(p => !p.IsFree).Select(p => p.FollowId).Distinct().Count();
            Assert.That(followed, Is.EqualTo(Mathf.Min(living, CameraPanels.MaxPanels)), "new panels watch residents nobody was watching");

            var extraCameras = game.Panels.All.Skip(1).Select(p => p.Camera).ToList();
            game.SetPanelCount(0);
            yield return null;
            Assert.That(game.Panels.Count, Is.EqualTo(1), "there is always at least one panel");
            Assert.That(Camera.main, Is.Not.Null, "the main camera is kept");
            Assert.That(extraCameras.All(c => c == null), Is.True, "removed panels' cameras are destroyed");
        }

        [UnityTest]
        public IEnumerator WatchEveryone_GivesEachLivingResidentAPanel()
        {
            game.WatchEveryone();
            yield return null;

            var living = game.Simulation.Residents.Where(r => r.IsAlive).Take(CameraPanels.MaxPanels).Select(r => r.Id).ToList();
            Assert.That(game.Panels.Count, Is.EqualTo(living.Count));
            CollectionAssert.AreEquivalent(living, game.Panels.All.Select(p => p.FollowId).ToList());
        }

        [UnityTest]
        public IEnumerator FollowPanel_StaysWithItsResident_IndoorsAndOut()
        {
            var resident = game.Simulation.Residents.First(r => r.IsAlive);
            game.Select(resident, followInActivePanel: true);
            var panel = game.Panels.Active;
            Assert.That(panel.FollowId, Is.EqualTo(resident.Id));

            var sawInside = false;
            var sawOutside = false;
            for (var minute = 0; minute < 2 * 24 * 60 && !(sawInside && sawOutside); minute += 5)
            {
                game.AdvanceMinutes(5);
                yield return null;

                var map = game.Simulation.World.GetMap(resident.Location.MapId);
                Assert.That(panel.MapId, Is.EqualTo(map.Id), $"day {game.Simulation.Time.Day} {game.Simulation.Time.ClockText}: the panel is on the resident's map");
                Assert.That(game.View.TryGetPosition(resident.Id, out var figure), Is.True);
                var camera = panel.Camera.transform.position;
                var halfWidth = panel.Camera.orthographicSize * panel.Camera.aspect;
                Assert.That(Mathf.Abs(figure.x - camera.x) <= halfWidth && Mathf.Abs(figure.y - camera.y) <= panel.Camera.orthographicSize, Is.True,
                    $"{resident.FirstName} at {figure} is outside the panel's view (camera {camera}, size {panel.Camera.orthographicSize}) on {map.Name}");

                sawInside |= !map.IsExterior;
                sawOutside |= map.IsExterior;
            }

            Assert.That(sawInside && sawOutside, Is.True, "the resident should go both indoors and out in two days");
            Assert.That(panel.FollowId, Is.EqualTo(resident.Id), "the panel never stopped following");
        }

        [UnityTest]
        public IEnumerator FreePanel_StaysOnItsMap_WhileTheTownMovesOn()
        {
            game.SetPanelCount(2);
            yield return null;
            var interior = game.Simulation.World.Maps.First(m => !m.IsExterior);
            game.Panels.All[1].LookAtMap(interior.Id, game.View);

            game.AdvanceMinutes(60);
            yield return null;

            var panel = game.Panels.All[1];
            Assert.That(panel.IsFree, Is.True);
            Assert.That(panel.MapId, Is.EqualTo(interior.Id));
            var (centre, _, _) = game.View.Bounds(interior.Id);
            Assert.That(Vector2.Distance(panel.Camera.transform.position, centre), Is.LessThan(0.01f), "a free camera starts over the middle of its map");
        }

        [UnityTest]
        public IEnumerator PlaceMenu_TakesAFollowingPanelOutside_AndStepsBothWays()
        {
            var places = CameraPanels.Places(game.Simulation.World);
            Assert.That(places.Count, Is.EqualTo(game.Simulation.World.Maps.Count()), "every map is a place");
            Assert.That(places[0].IsExterior, Is.True, "the village comes first");

            var someone = game.Simulation.Residents.First(r => r.IsAlive);
            game.Select(someone, followInActivePanel: true);
            var index = game.Panels.ActiveIndex;
            var panel = game.Panels.All[index];
            game.Panels.ShowPlace(index, places[0].Id, game.View);
            yield return null;

            Assert.That(panel.IsFree, Is.True);
            Assert.That(panel.MapId, Is.EqualTo(places[0].Id));
            var (centre, _, _) = game.View.Bounds(places[0].Id);
            Assert.That(Vector2.Distance(panel.Camera.transform.position, centre), Is.LessThan(0.01f));

            game.Panels.CycleMap(index, game.View, game.Simulation, -1);
            Assert.That(panel.MapId, Is.EqualTo(places[places.Count - 1].Id), "stepping back from the first place wraps to the last");
            game.Panels.CycleMap(index, game.View, game.Simulation, 1);
            Assert.That(panel.MapId, Is.EqualTo(places[0].Id));

            game.Panels.ShowPlace(index, "no-such-map", game.View);
            Assert.That(panel.MapId, Is.EqualTo(places[0].Id), "an unknown place is ignored");
        }
    }
}
