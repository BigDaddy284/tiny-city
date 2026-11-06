using System.Collections;
using System.Linq;
using NUnit.Framework;
using TinyCity.Runtime;
using TinyCity.Simulation.Content;
using UnityEngine;
using UnityEngine.TestTools;

namespace TinyCity.Tests.PlayMode
{
    /// <summary>The observer's one camera (owner, 5 November: one camera only).</summary>
    public class TownCameraTests
    {
        private GameController game;

        [UnitySetUp]
        public IEnumerator FindGame()
        {
            yield return null;
            yield return null;
            game = Object.FindAnyObjectByType<GameController>();
            Assert.That(game, Is.Not.Null, "GameController was not created.");
            Assert.That(game.TownCamera, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator HidingTheSidePanel_LetsTheCityFillTheWindow()
        {
            var wasHidden = game.SidebarHidden;
            if (wasHidden)
            {
                game.ToggleSidebar();
            }

            yield return null;
            var withPanel = game.TownCamera.ScreenRect.width;
            Assert.That(withPanel, Is.LessThan(Screen.width - 300f), "the side panel takes its share");

            game.ToggleSidebar();
            yield return null;
            Assert.That(game.SidebarHidden, Is.True);
            Assert.That(game.TownCamera.ScreenRect.width, Is.GreaterThan(withPanel + 300f), "the camera takes the whole width");
            Assert.That(PlayerPrefs.GetInt("TinyCity.SidebarHidden", 0), Is.EqualTo(1), "remembered for next time");

            game.ToggleSidebar();
            yield return null;
            Assert.That(game.TownCamera.ScreenRect.width, Is.EqualTo(withPanel).Within(1f));
            if (wasHidden)
            {
                game.ToggleSidebar();
            }
        }

        [UnityTest]
        public IEnumerator TheOnlyCamera_IsTheMainCamera_OverTheWholeLand()
        {
            game.TownCamera.ShowWholeLand(game.View, game.Simulation);
            yield return null;

            Assert.That(game.TownCamera.Camera, Is.SameAs(Camera.main));
            Assert.That(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length, Is.EqualTo(1), "there is one camera");
            Assert.That(game.TownCamera.IsFree, Is.True);
            Assert.That(game.TownCamera.MapId, Is.EqualTo(FlatLand.MapId));
            var (centre, _, _) = game.View.Bounds(FlatLand.MapId);
            Assert.That(Vector2.Distance(game.TownCamera.Camera.transform.position, centre), Is.LessThan(0.01f), "it looks at the middle of the land");
        }

        [UnityTest]
        public IEnumerator FollowingCamera_StaysWithItsResident()
        {
            var resident = game.Simulation.Residents.First(r => r.IsAlive);
            game.Select(resident, follow: true);
            var cam = game.TownCamera;
            Assert.That(cam.FollowId, Is.EqualTo(resident.Id));

            for (var minute = 0; minute < 60; minute += 5)
            {
                game.AdvanceMinutes(5);
                yield return null;

                Assert.That(cam.MapId, Is.EqualTo(resident.Location.MapId), "the camera is on the resident's map");
                Assert.That(game.View.TryGetPosition(resident.Id, out var figure), Is.True);
                var camera = cam.Camera.transform.position;
                var halfWidth = cam.Camera.orthographicSize * cam.Camera.aspect;
                Assert.That(Mathf.Abs(figure.x - camera.x) <= halfWidth && Mathf.Abs(figure.y - camera.y) <= cam.Camera.orthographicSize, Is.True,
                    $"{resident.FirstName} at {figure} is outside the view (camera {camera}, size {cam.Camera.orthographicSize})");
            }

            Assert.That(cam.FollowId, Is.EqualTo(resident.Id), "the camera never stopped following");
        }

        [UnityTest]
        public IEnumerator Release_LeavesAFreeCameraWhereTheViewWas()
        {
            var resident = game.Simulation.Residents.First(r => r.IsAlive);
            game.Select(resident, follow: true);
            yield return null;
            var cam = game.TownCamera;
            var before = cam.Camera.transform.position;
            var size = cam.Camera.orthographicSize;

            cam.Release(game.View);
            game.AdvanceMinutes(30);
            yield return null;

            Assert.That(cam.IsFree, Is.True);
            Assert.That(Vector2.Distance(cam.Camera.transform.position, before), Is.LessThan(0.01f), "the camera stays put");
            Assert.That(cam.Camera.orthographicSize, Is.EqualTo(size).Within(0.01f), "and keeps its zoom");
        }

        [UnityTest]
        public IEnumerator CycleResident_StepsThroughTheLiving_BothWays()
        {
            var living = game.Simulation.Residents.Where(r => r.IsAlive).ToList();
            var cam = game.TownCamera;
            cam.Follow(living[0].Id, game.Simulation);
            cam.CycleResident(1, game.Simulation);
            Assert.That(cam.FollowId, Is.EqualTo(living[1 % living.Count].Id));
            cam.CycleResident(-1, game.Simulation);
            cam.CycleResident(-1, game.Simulation);
            Assert.That(cam.FollowId, Is.EqualTo(living[living.Count - 1].Id), "stepping back from the first wraps to the last");
            yield return null;
        }
    }
}
