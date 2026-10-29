using System;

namespace TinyCity.Sound
{
    /// <summary>The background sound of where the camera is: the city by day, the city at night, or indoors.</summary>
    public enum AmbienceKind
    {
        CityDay,
        CityNight,
        Indoors,
    }

    /// <summary>
    /// City ambience made from filtered noise: a low hum, passing hover-traffic swells and, at night, the faint buzz
    /// of neon. Indoors is a quiet room tone with an air unit. Each loop is 24 seconds and joins without a seam.
    /// </summary>
    public static class Ambience
    {
        public const double LoopSeconds = 24;
        private const double OverlapSeconds = 3;

        public static SoundBuffer Make(AmbienceKind kind)
        {
            var loopLength = Synth.Samples(LoopSeconds);
            var total = loopLength + Synth.Samples(OverlapSeconds);
            var hum = new double[total];
            var traffic = new double[total];
            var detail = new double[total];

            var seed = 100 + (int)kind;
            for (var i = 0; i < total; i++)
            {
                hum[i] = Synth.Noise(seed, i);
            }

            Synth.LowPass(hum, kind == AmbienceKind.Indoors ? 140 : 220);
            Synth.LowPass(hum, kind == AmbienceKind.Indoors ? 180 : 300);

            switch (kind)
            {
                case AmbienceKind.CityDay:
                    AddTraffic(traffic, seed, passes: 9, level: 1.0);
                    AddChime(detail, 15.5, new[] { 76.0, 83.0 }, 0.05);
                    break;
                case AmbienceKind.CityNight:
                    AddTraffic(traffic, seed, passes: 4, level: 0.7);
                    AddNeonBuzz(detail, seed);
                    break;
                default:
                    AddAirUnit(detail);
                    break;
            }

            var humLevel = kind == AmbienceKind.Indoors ? 3.0 : kind == AmbienceKind.CityNight ? 3.4 : 4.0;
            var mix = new double[total];
            for (var i = 0; i < total; i++)
            {
                mix[i] = (hum[i] * humLevel) + traffic[i] + detail[i];
            }

            var loop = Synth.Crossfade(mix, loopLength, Synth.Samples(OverlapSeconds));
            var peak = kind == AmbienceKind.Indoors ? 0.25 : 0.45;
            return new SoundBuffer(KeyOf(kind), Synth.Normalise(loop, peak), Synth.Rate, loops: true);
        }

        public static string KeyOf(AmbienceKind kind) =>
            kind == AmbienceKind.CityNight ? "ambience-night" : kind == AmbienceKind.Indoors ? "ambience-indoors" : "ambience-day";

        /// <summary>Hover cars passing: band-limited noise that swells and fades, its pitch sweeping as it goes by.</summary>
        private static void AddTraffic(double[] into, int seed, int passes, double level)
        {
            for (var p = 0; p < passes; p++)
            {
                var start = Synth.Pick(seed, p * 3) * (LoopSeconds - 1);
                var length = 2.5 + (Synth.Pick(seed, (p * 3) + 1) * 3.0);
                var loudness = (0.4 + (0.6 * Synth.Pick(seed, (p * 3) + 2))) * level;
                var first = Synth.Samples(start);
                var count = Synth.Samples(length);
                var low = 0.0;
                var smooth = 0.0;
                var lower = 0.0;
                for (var i = 0; i < count; i++)
                {
                    var t = (double)i / Synth.Rate;
                    var progress = t / length;

                    // The note of the engine rises as it nears and falls as it leaves.
                    var cutoff = 250 + (900 * Math.Sin(Math.PI * progress));
                    low += Synth.LowPassFactor(cutoff) * (Synth.Noise(seed + 50 + p, i) - low);
                    smooth += Synth.LowPassFactor(cutoff) * (low - smooth);
                    lower += Synth.LowPassFactor(120) * (smooth - lower);
                    var index = (first + i) % into.Length;
                    into[index] += (smooth - lower) * Synth.Bell(t, length) * loudness * 3.0;
                }
            }
        }

        private static void AddChime(double[] into, double start, double[] notes, double level)
        {
            for (var n = 0; n < notes.Length; n++)
            {
                var hz = Synth.Hz(notes[n]);
                var first = Synth.Samples(start + (n * 0.35));
                var count = Synth.Samples(2.5);
                for (var i = 0; i < count && first + i < into.Length; i++)
                {
                    var t = (double)i / Synth.Rate;
                    into[first + i] += Synth.Sine(hz * t) * Synth.Pluck(t, 0.01, 0.5) * level;
                }
            }
        }

        /// <summary>The faint mains buzz of neon tubes, flickering now and then.</summary>
        private static void AddNeonBuzz(double[] into, int seed)
        {
            for (var i = 0; i < into.Length; i++)
            {
                var t = (double)i / Synth.Rate;
                var flicker = 0.75 + (0.25 * Math.Sin(2 * Math.PI * 0.13 * t)) + (Synth.Pick(seed, i / 2205) < 0.04 ? -0.4 : 0);
                var buzz = (0.6 * Synth.Sine(120 * t)) + (0.3 * Synth.Sine(240 * t)) + (0.15 * Synth.Sine(360 * t));
                into[i] += buzz * flicker * 0.018;
            }
        }

        /// <summary>A soft air unit: a low hum with a little hiss.</summary>
        private static void AddAirUnit(double[] into)
        {
            for (var i = 0; i < into.Length; i++)
            {
                var t = (double)i / Synth.Rate;
                into[i] += ((0.7 * Synth.Sine(100 * t)) + (0.2 * Synth.Sine(200 * t))) * 0.02;
                into[i] += Synth.Noise(170, i) * 0.006;
            }
        }
    }
}
