using System.Collections;
using NUnit.Framework;
using TinyCity.Runtime;
using TinyCity.Simulation.AI;
using UnityEngine;
using UnityEngine.TestTools;

namespace TinyCity.Tests.PlayMode
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

            // CI has no Ollama, and in Tiny City 2 nobody moves without it, so the day is lived by the old routine.
            var aiRunsEverything = game.Simulation.AiRunsEverything;
            game.Simulation.AiRunsEverything = false;
            try
            {
                var before = game.Simulation.Time.TotalMinutes;
                game.AdvanceMinutes(180);
                yield return null;

                Assert.That(game.Simulation.Time.TotalMinutes - before, Is.GreaterThanOrEqualTo(180));
                Assert.That(game.Simulation.Residents[0].Log, Is.Not.Empty);
            }
            finally
            {
                game.Simulation.AiRunsEverything = aiRunsEverything;
            }
        }

        /// <summary>
        /// The CI runner has no Ollama, so this checks the real web request path reports a refused connection and
        /// Tiny City 2 has no built-in routine, so the residents wait for the AI and the clock holds.
        /// </summary>
        [UnityTest]
        public IEnumerator WithoutOllama_AiReportsOffline_AndTheTownWaitsForIt()
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
            Assert.That(game.Simulation.Residents[0].State, Is.EqualTo(TinyCity.Simulation.Residents.ResidentState.Deciding));
            Assert.That(game.HoldingForAi, Is.True);
        }
    }
}
