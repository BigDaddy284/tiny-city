using System.Collections;
using NUnit.Framework;
using TinyCity.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace TinyCity.Tests.PlayMode
{
    /// <summary>Sound in the running game: everything is made, music and ambience play, and the volume sliders work.</summary>
    public class SoundPlayTests
    {
        private static IEnumerator WaitUntilReady(GameController game)
        {
            var waited = 0f;
            while (!game.Sound.Ready && waited < 30f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.That(game.Sound.Ready, Is.True, "every sound is made within 30 seconds");
        }

        [UnityTest]
        public IEnumerator MusicAndAmbience_Play_AndFollowTheVolume()
        {
            yield return null;
            var game = Object.FindAnyObjectByType<GameController>();
            yield return WaitUntilReady(game);
            yield return null;

            Assert.That(game.Sound.MusicPlaying, Does.StartWith("music-"));
            Assert.That(game.Sound.AmbiencePlaying, Does.StartWith("ambience-"));

            var keep = game.Preferences.MusicVolume;
            try
            {
                game.Preferences.MusicVolume = 0;
                yield return null;
                Assert.That(game.Sound.MusicVolume, Is.EqualTo(0f));
            }
            finally
            {
                game.Preferences.MusicVolume = keep;
            }
        }

        [UnityTest]
        public IEnumerator ResidentsMakeSounds_AsTheTownRuns()
        {
            yield return null;
            var game = Object.FindAnyObjectByType<GameController>();
            yield return WaitUntilReady(game);
            var before = game.Sound.EffectsPlayed;
            for (var i = 0; i < 240 && game.Sound.EffectsPlayed == before; i++)
            {
                game.AdvanceMinutes(5);
                yield return null;
            }

            Assert.That(game.Sound.EffectsPlayed, Is.GreaterThan(before), "someone near the camera does something audible");
        }
    }
}
