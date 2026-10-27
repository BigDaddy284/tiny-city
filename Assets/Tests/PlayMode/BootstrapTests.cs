using System.Collections;
using NUnit.Framework;
using TinyTown.Runtime;
using TinyTown.Simulation.AI;
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

        /// <summary>
        /// The CI runner has no Ollama, so this checks the real web request path reports a refused connection and
        /// the town carries on with its built-in routine.
        /// </summary>
        [UnityTest]
        public IEnumerator WithoutOllama_AiReportsOffline_AndTheTownKeepsRunning()
        {
            yield return null;
            var game = Object.FindAnyObjectByType<GameController>();
            Assert.That(game.Ai, Is.Not.Null);

            var waitUntil = Time.realtimeSinceStartup + (float)AiService.CheckTimeoutSeconds + 5f;
            while ((game.Ai.Status == AiConnectionStatus.Checking || game.Ai.Status == AiConnectionStatus.LoadingModel) && Time.realtimeSinceStartup < waitUntil)
            {
                yield return null;
            }

            Assert.That(game.Ai.Status, Is.EqualTo(AiConnectionStatus.Offline), game.Ai.Summary());
            Assert.That(game.Ai.StatusDetail, Is.Not.Empty);
            Assert.That(game.Simulation.AiDecisionsEnabled, Is.False);

            game.AdvanceMinutes(120);
            Assert.That(game.Simulation.Residents[0].State, Is.Not.EqualTo(TinyTown.Simulation.Residents.ResidentState.Deciding));
        }
    }
}
