using System;

namespace TinyCity.Sound
{
    /// <summary>
    /// The building blocks every generated sound is made from: notes, oscillators, envelopes, filters and noise.
    /// Noise comes from a hash of the sample number, never <see cref="Random"/>, so each sound is the same every time.
    /// </summary>
    public static class Synth
    {
        public const int Rate = SoundBuffer.GeneratedRate;

        private const int TableSize = 4096;

        /// <summary>A soft, rounded sawtooth (its first eight harmonics, gently rolled off) for pads.</summary>
        private static readonly double[] SoftSawTable = BuildTable(n => n > 8 ? 0 : (1.0 / n) * Math.Pow(0.82, n - 1));

        /// <summary>A mellow square-ish tone (odd harmonics) for plucks and beeps.</summary>
        private static readonly double[] HollowTable = BuildTable(n => n % 2 == 0 || n > 7 ? 0 : 1.0 / (n * n * 0.6));

        /// <summary>The frequency of a MIDI note (69 = A4 = 440 Hz).</summary>
        public static double Hz(double midiNote) => 440.0 * Math.Pow(2.0, (midiNote - 69.0) / 12.0);

        public static int Samples(double seconds) => (int)Math.Round(seconds * Rate);

        public static double Sine(double phase) => Math.Sin(phase * 2.0 * Math.PI);

        public static double SoftSaw(double phase) => Lookup(SoftSawTable, phase);

        public static double Hollow(double phase) => Lookup(HollowTable, phase);

        /// <summary>A rise over <paramref name="attack"/> seconds, then an exponential fall that halves every <paramref name="halfLife"/> seconds.</summary>
        public static double Pluck(double t, double attack, double halfLife)
        {
            if (t < 0)
            {
                return 0;
            }

            return t < attack ? t / attack : Math.Pow(0.5, (t - attack) / halfLife);
        }

        /// <summary>A smooth swell: rises over <paramref name="attack"/>, holds, and fades out over <paramref name="release"/> after <paramref name="length"/>.</summary>
        public static double Swell(double t, double length, double attack, double release)
        {
            if (t < 0 || t > length + release)
            {
                return 0;
            }

            var up = t < attack ? Smooth(t / attack) : 1.0;
            var down = t > length ? 1.0 - Smooth((t - length) / release) : 1.0;
            return up * down;
        }

        /// <summary>A bell-shaped bump from 0 to 1 and back over the span.</summary>
        public static double Bell(double t, double length) => t < 0 || t > length ? 0 : Math.Sin(Math.PI * t / length) * Math.Sin(Math.PI * t / length);

        public static double Smooth(double x)
        {
            x = Math.Max(0, Math.Min(1, x));
            return x * x * (3 - (2 * x));
        }

        /// <summary>White noise from -1 to 1 for sample <paramref name="i"/> of stream <paramref name="seed"/>.</summary>
        public static double Noise(int seed, int i) => (Hash(seed, i) / (double)uint.MaxValue * 2.0) - 1.0;

        /// <summary>A number from 0 to 1 picked by hash, for choosing times, notes and variations.</summary>
        public static double Pick(int seed, int i) => Hash(seed, i ^ 0x5bd1e995) / (double)uint.MaxValue;

        /// <summary>FNV-1a over two integers.</summary>
        public static uint Hash(int a, int b)
        {
            var h = 2166136261u;
            for (var k = 0; k < 4; k++)
            {
                h = (h ^ (uint)((a >> (8 * k)) & 0xff)) * 16777619u;
            }

            for (var k = 0; k < 4; k++)
            {
                h = (h ^ (uint)((b >> (8 * k)) & 0xff)) * 16777619u;
            }

            // A final mix so neighbouring samples don't correlate.
            h ^= h >> 15;
            h *= 0x2c1b3c6du;
            h ^= h >> 12;
            return h;
        }

        /// <summary>The smoothing factor for a one-pole low-pass filter at <paramref name="cutoff"/> Hz.</summary>
        public static double LowPassFactor(double cutoff) => 1.0 - Math.Exp(-2.0 * Math.PI * Math.Max(1.0, cutoff) / Rate);

        /// <summary>Filters a whole buffer in place with a one-pole low-pass.</summary>
        public static void LowPass(double[] samples, double cutoff)
        {
            var a = LowPassFactor(cutoff);
            var y = 0.0;
            for (var i = 0; i < samples.Length; i++)
            {
                y += a * (samples[i] - y);
                samples[i] = y;
            }
        }

        /// <summary>Removes the low end in place (the signal minus its low-passed self).</summary>
        public static void HighPass(double[] samples, double cutoff)
        {
            var a = LowPassFactor(cutoff);
            var y = 0.0;
            for (var i = 0; i < samples.Length; i++)
            {
                y += a * (samples[i] - y);
                samples[i] -= y;
            }
        }

        /// <summary>A feedback echo, as on the music's plucked notes.</summary>
        public static void Echo(double[] samples, double delaySeconds, double feedback, double mix)
        {
            var delay = Math.Max(1, Samples(delaySeconds));
            var line = new double[samples.Length];
            for (var i = 0; i < samples.Length; i++)
            {
                var echoed = i >= delay ? line[i - delay] : 0;
                line[i] = samples[i] + (echoed * feedback);
                samples[i] += echoed * mix;
            }
        }

        /// <summary>
        /// Makes a loop out of a sound rendered past its end: whatever rings on after <paramref name="loopLength"/>
        /// (echoes, fading notes) is added back onto the start, so the loop joins without a gap.
        /// </summary>
        public static double[] WrapTail(double[] rendered, int loopLength)
        {
            var loop = new double[loopLength];
            Array.Copy(rendered, loop, Math.Min(loopLength, rendered.Length));
            for (var i = loopLength; i < rendered.Length; i++)
            {
                loop[(i - loopLength) % loopLength] += rendered[i];
            }

            return loop;
        }

        /// <summary>
        /// Makes a loop out of a continuous sound (noise, hum) rendered <paramref name="overlap"/> samples past its
        /// end: the start fades in while the overrun fades out, so the last sample leads straight into the first.
        /// </summary>
        public static double[] Crossfade(double[] rendered, int loopLength, int overlap)
        {
            var loop = new double[loopLength];
            Array.Copy(rendered, loop, loopLength);
            overlap = Math.Min(overlap, Math.Min(loopLength, rendered.Length - loopLength));
            for (var i = 0; i < overlap; i++)
            {
                var fade = (double)i / overlap;
                loop[i] = (rendered[i] * fade) + (rendered[loopLength + i] * (1 - fade));
            }

            return loop;
        }

        /// <summary>Scales to the given peak and converts to the samples a <see cref="SoundBuffer"/> holds.</summary>
        public static float[] Normalise(double[] samples, double peak)
        {
            var loudest = 0.0;
            foreach (var s in samples)
            {
                loudest = Math.Max(loudest, Math.Abs(s));
            }

            var gain = loudest > 0 ? peak / loudest : 0;
            var result = new float[samples.Length];
            for (var i = 0; i < samples.Length; i++)
            {
                result[i] = (float)(samples[i] * gain);
            }

            return result;
        }

        private static double[] BuildTable(Func<int, double> harmonic)
        {
            var table = new double[TableSize];
            var peak = 0.0;
            for (var i = 0; i < TableSize; i++)
            {
                var phase = (double)i / TableSize;
                var sum = 0.0;
                for (var n = 1; n <= 16; n++)
                {
                    var amplitude = harmonic(n);
                    if (amplitude != 0)
                    {
                        sum += amplitude * Math.Sin(2.0 * Math.PI * n * phase);
                    }
                }

                table[i] = sum;
                peak = Math.Max(peak, Math.Abs(sum));
            }

            for (var i = 0; i < TableSize; i++)
            {
                table[i] /= peak;
            }

            return table;
        }

        private static double Lookup(double[] table, double phase)
        {
            phase -= Math.Floor(phase);
            var position = phase * TableSize;
            var index = (int)position;
            var next = (index + 1) % TableSize;
            var frac = position - index;
            return table[index % TableSize] + ((table[next] - table[index % TableSize]) * frac);
        }
    }
}
