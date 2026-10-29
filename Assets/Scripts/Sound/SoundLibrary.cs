using System;
using System.Collections.Generic;

namespace TinyCity.Sound
{
    /// <summary>
    /// Every sound the game plays, by key. Each is generated in code; a WAV file named after its key
    /// (for example <c>music-day.wav</c>) can replace it, as described in <c>Documentation/SoundOverrides.md</c>.
    /// </summary>
    public static class SoundLibrary
    {
        public static IEnumerable<string> Keys
        {
            get
            {
                foreach (MusicPiece piece in Enum.GetValues(typeof(MusicPiece)))
                {
                    yield return Music.KeyOf(piece);
                }

                foreach (AmbienceKind kind in Enum.GetValues(typeof(AmbienceKind)))
                {
                    yield return Ambience.KeyOf(kind);
                }

                foreach (SoundEffect effect in Enum.GetValues(typeof(SoundEffect)))
                {
                    for (var v = 0; v < SoundEffects.VariantsOf(effect); v++)
                    {
                        yield return SoundEffects.KeyOf(effect, v);
                    }
                }
            }
        }

        /// <summary>Makes the sound for a key, or null if there is no such sound.</summary>
        public static SoundBuffer Generate(string key)
        {
            foreach (MusicPiece piece in Enum.GetValues(typeof(MusicPiece)))
            {
                if (key == Music.KeyOf(piece))
                {
                    return Music.Compose(piece);
                }
            }

            foreach (AmbienceKind kind in Enum.GetValues(typeof(AmbienceKind)))
            {
                if (key == Ambience.KeyOf(kind))
                {
                    return Ambience.Make(kind);
                }
            }

            foreach (SoundEffect effect in Enum.GetValues(typeof(SoundEffect)))
            {
                for (var v = 0; v < SoundEffects.VariantsOf(effect); v++)
                {
                    if (key == SoundEffects.KeyOf(effect, v))
                    {
                        return SoundEffects.Make(effect, v);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// The sound for a key: the replacement if <paramref name="replacement"/> gives readable WAV bytes, otherwise
        /// the generated one. A file that can't be read is reported through <paramref name="problem"/> and ignored.
        /// </summary>
        public static SoundBuffer Load(string key, Func<string, byte[]> replacement, Action<string> problem = null)
        {
            var bytes = replacement?.Invoke(key);
            if (bytes != null)
            {
                try
                {
                    var (samples, rate) = Wav.Read(bytes);
                    return new SoundBuffer(key, samples, rate, IsLoop(key));
                }
                catch (Exception e) when (e is FormatException || e is ArgumentException)
                {
                    problem?.Invoke($"{key}.wav could not be used ({e.Message}); playing the built-in sound.");
                }
            }

            return Generate(key);
        }

        public static bool IsLoop(string key) => key.StartsWith("music-", StringComparison.Ordinal) || key.StartsWith("ambience-", StringComparison.Ordinal);
    }
}
