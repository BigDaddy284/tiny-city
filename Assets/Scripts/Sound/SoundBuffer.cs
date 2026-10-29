using System;

namespace TinyCity.Sound
{
    /// <summary>A mono sound as samples from -1 to 1: a piece of music, an ambience loop or a short effect.</summary>
    public sealed class SoundBuffer
    {
        /// <summary>The rate every generated sound uses. Replacement files keep their own.</summary>
        public const int GeneratedRate = 22050;

        public SoundBuffer(string key, float[] samples, int sampleRate, bool loops)
        {
            if (samples == null || samples.Length == 0)
            {
                throw new ArgumentException($"Sound '{key}' has no samples.");
            }

            if (sampleRate <= 0)
            {
                throw new ArgumentException($"Sound '{key}' has an invalid sample rate.");
            }

            Key = key;
            Samples = samples;
            SampleRate = sampleRate;
            Loops = loops;
        }

        /// <summary>The name it is played and replaced by, such as <c>music-day</c> or <c>sfx-door</c>.</summary>
        public string Key { get; }

        public float[] Samples { get; }

        public int SampleRate { get; }

        /// <summary>Music and ambience loop seamlessly; effects play once.</summary>
        public bool Loops { get; }

        public double Seconds => Samples.Length / (double)SampleRate;

        /// <summary>The loudest sample, ignoring sign.</summary>
        public double Peak
        {
            get
            {
                var peak = 0.0;
                foreach (var s in Samples)
                {
                    peak = Math.Max(peak, Math.Abs(s));
                }

                return peak;
            }
        }
    }
}
