using System.Collections;
using NUnit.Framework;
using TinyTown.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace TinyTown.Tests.PlayMode
{
    public class BootstrapTests
    {
        [UnityTest]
        public IEnumerator EnteringPlayMode_CreatesBootstrapCameraAndValidSettings()
        {
            yield return null;

            var bootstrap = Object.FindAnyObjectByType<Bootstrap>();
            Assert.That(bootstrap, Is.Not.Null, "Bootstrap was not created on startup.");
            Assert.That(bootstrap.gameObject.name, Is.EqualTo(Bootstrap.ObjectName));
            Assert.That(Camera.main, Is.Not.Null, "No main camera exists after startup.");
            Assert.That(bootstrap.Settings, Is.Not.Null);
            Assert.That(bootstrap.Settings.Validate(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator Game_StartsTown_DrawsMap_AndResidentLivesTheirDay()
        {
            yield return null;
            yield return null;

            var game = Object.FindAnyObjectByType<GameController>();
            Assert.That(game, Is.Not.Null, "GameController was not created.");
            Assert.That(game.Simulation, Is.Not.Null);
            Assert.That(Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Length, Is.GreaterThan(50), "Map was not drawn.");

            var before = game.Simulation.Time.TotalMinutes;
            game.AdvanceMinutes(180);
            yield return null;

            Assert.That(game.Simulation.Time.TotalMinutes - before, Is.GreaterThanOrEqualTo(180));
            Assert.That(game.Simulation.Residents[0].Log, Is.Not.Empty);
        }
    }
}
