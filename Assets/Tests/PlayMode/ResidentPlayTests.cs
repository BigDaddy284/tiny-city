using System.Collections;
using System.Linq;
using NUnit.Framework;
using TinyTown.Runtime;
using TinyTown.Simulation.Residents;
using UnityEngine;
using UnityEngine.TestTools;

namespace TinyTown.Tests.PlayMode
{
    /// <summary>Phase 6, build 2: the residents drawn in the town, animated, with their moods.</summary>
    public class ResidentPlayTests
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
        public void EveryLivingResident_IsDrawn_WithCrispPixels()
        {
            var living = game.Simulation.Residents.Where(r => r.IsAlive).ToList();
            Assert.That(living.Count, Is.GreaterThan(0));
            foreach (var r in living)
            {
                Assert.That(game.View.ResidentRenderers.TryGetValue(r.Id, out var body), Is.True, r.FirstName);
                Assert.That(body.enabled && body.sprite != null, Is.True, $"{r.FirstName} is shown");
                Assert.That(body.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(body.sortingOrder, Is.GreaterThan(1000), "residents stand among the furniture, above the ground");
            }
        }

        [UnityTest]
        public IEnumerator AHungryResident_ShowsItOverTheirHead()
        {
            var r = game.Simulation.Residents.First(x => x.IsAlive && x.State != ResidentState.Performing);
            for (var i = 0; i < Needs.Count; i++)
            {
                r.Needs[(NeedType)i] = 60;
            }

            r.Needs[NeedType.Hunger] = 5;
            game.View.UpdateResidents(game.Simulation, 0f);
            var icon = game.View.MoodIcons[r.Id];
            Assert.That(icon.enabled && icon.sprite != null, Is.True, "a mood icon is shown");
            Assert.That(icon.sprite.name, Is.EqualTo("Mood Hungry"));
            Assert.That(icon.transform.position.y, Is.GreaterThan(game.View.ResidentRenderers[r.Id].transform.position.y + 0.4f), "above the head");
            yield return null;
        }
    }
}
