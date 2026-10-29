using System.Collections;
using System.Linq;
using NUnit.Framework;
using TinyCity.Runtime;
using TinyCity.Simulation.Time;
using UnityEngine;
using UnityEngine.TestTools;

namespace TinyCity.Tests.PlayMode
{
    /// <summary>Phase 6, build 1: the generated places on screen, the seasons and the light.</summary>
    public class ArtPlayTests
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

        [Test]
        public void EveryMap_IsDrawn_WithCrispSprites()
        {
            var view = game.View;
            Assert.That(view.MapViews.Count, Is.EqualTo(game.Simulation.World.Maps.Count()));
            foreach (var map in view.MapViews.Values)
            {
                Assert.That(map.PieceCount, Is.GreaterThan(5), map.Map.Id);
                var renderers = map.GetComponentsInChildren<SpriteRenderer>(true);
                Assert.That(renderers.All(r => r.sprite != null && r.sprite.texture.filterMode == FilterMode.Point), Is.True, $"{map.Map.Id} pixels stay sharp");
                Assert.That(renderers.Any(r => r.sortingOrder == 0), Is.True, "the ground sits behind everything");
            }
        }

        [UnityTest]
        public IEnumerator Night_DarkensTheVillage_AndLightsTheWindows()
        {
            var village = game.View.MapViews.Values.First(m => m.Map.IsExterior);
            var day = GameTime.FromDayAndTime(game.Simulation.Time.Day, 12, 0);
            // The game re-applies its own clock every frame, so each check follows its ApplyTime directly.
            game.View.ApplyTime(day);
            var windows = village.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.name.StartsWith("Windows")).ToList();
            Assert.That(windows.Count, Is.GreaterThan(14));
            Assert.That(windows.All(w => !w.enabled), Is.True, "no lit windows by day");
            Assert.That(village.CurrentLight.Value.WindowGlow, Is.EqualTo(0));

            game.View.ApplyTime(GameTime.FromDayAndTime(game.Simulation.Time.Day, 23, 30));
            Assert.That(windows.All(w => w.enabled), Is.True, "windows are lit at night");
            var ground = village.GetComponentsInChildren<SpriteRenderer>(true).First(r => r.sortingOrder == 0);
            Assert.That(ground.color.r < 0.7f && ground.color.b > ground.color.r, Is.True, $"night is dark and blue, was {ground.color}");

            // Back to the game's own time.
            game.View.ApplyTime(game.Simulation.Time);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TurningSeason_RedrawsTheMaps()
        {
            var now = game.Simulation.Time;
            var season = game.View.Season;
            var other = GameTime.FromDayAndTime(now.Day + Seasons.DaysPerSeason, 12, 0);
            Assert.That(Seasons.Of(other), Is.Not.EqualTo(season));

            game.View.ApplyTime(other);
            Assert.That(game.View.Season, Is.EqualTo(Seasons.Of(other)));
            Assert.That(game.View.MapViews.Values.All(m => m.PieceCount > 5), Is.True);

            game.View.ApplyTime(now);
            Assert.That(game.View.Season, Is.EqualTo(season));
            yield return null;
        }
    }
}
