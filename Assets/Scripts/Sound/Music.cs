using System;

namespace TinyCity.Sound
{
    /// <summary>Which piece of music plays: a brighter one by day and a slower, dreamier one in the evening and at night.</summary>
    public enum MusicPiece
    {
        Day,
        Night,
    }

    /// <summary>
    /// Gentle synth music, written note by note in code: a soft pad holding each chord, a low bass, a plucked
    /// arpeggio with an echo, and (by day) quiet ticking hats. Each piece is 16 bars that loop without a seam.
    /// </summary>
    public static class Music
    {
        public const int Bars = 16;
        private const int BarsPerChord = 2;
        private const double TailSeconds = 6;

        private sealed class Score
        {
            public double Tempo;
            public int[][] Chords;
            public double PadCutoff;
            public double PadLevel;
            public double ArpLevel;
            public double ArpRestChance;
            public bool ArpEighths;
            public bool Hats;
            public int Seed;
        }

        // Each chord lists its notes (MIDI) from the bottom; the first is the root.
        private static readonly Score DayScore = new Score
        {
            Tempo = 80,
            Chords = new[]
            {
                new[] { 53, 57, 60, 64 }, // F major 7
                new[] { 55, 59, 62, 64 }, // G6
                new[] { 52, 55, 59, 62 }, // E minor 7
                new[] { 57, 60, 64, 67 }, // A minor 7
                new[] { 50, 53, 57, 60 }, // D minor 7
                new[] { 55, 59, 62, 65 }, // G7
                new[] { 48, 52, 55, 59 }, // C major 7
                new[] { 52, 56, 59, 62 }, // E7
            },
            PadCutoff = 1500,
            PadLevel = 0.09,
            ArpLevel = 0.16,
            ArpRestChance = 0.12,
            ArpEighths = true,
            Hats = true,
            Seed = 11,
        };

        private static readonly Score NightScore = new Score
        {
            Tempo = 68,
            Chords = new[]
            {
                new[] { 57, 60, 64, 71 }, // A minor 9
                new[] { 53, 57, 60, 64 }, // F major 7
                new[] { 50, 53, 57, 64 }, // D minor 9
                new[] { 52, 57, 59, 64 }, // E sus4
                new[] { 57, 60, 64, 67 }, // A minor 7
                new[] { 48, 52, 55, 59 }, // C major 7
                new[] { 53, 57, 60, 67 }, // F add 9
                new[] { 52, 56, 59, 62 }, // E7
            },
            PadCutoff = 900,
            PadLevel = 0.11,
            ArpLevel = 0.13,
            ArpRestChance = 0.35,
            ArpEighths = false,
            Hats = false,
            Seed = 23,
        };

        public static SoundBuffer Compose(MusicPiece piece)
        {
            var score = piece == MusicPiece.Night ? NightScore : DayScore;
            var beat = 60.0 / score.Tempo;
            var bar = beat * 4;
            var loopLength = Synth.Samples(Bars * bar);
            var total = loopLength + Synth.Samples(TailSeconds);

            var pad = new double[total];
            var bass = new double[total];
            var arp = new double[total];
            var hats = new double[total];

            for (var c = 0; c < Bars / BarsPerChord; c++)
            {
                var chord = score.Chords[c % score.Chords.Length];
                var start = c * BarsPerChord * bar;
                var length = BarsPerChord * bar;

                for (var n = 0; n < chord.Length; n++)
                {
                    // Two slightly detuned voices per note give the pad its width.
                    AddPadNote(pad, start, length, chord[n] - 0.06, score.PadLevel, n * 0.13);
                    AddPadNote(pad, start, length, chord[n] + 0.06, score.PadLevel, (n * 0.13) + 0.5);
                }

                for (var b = 0; b < BarsPerChord; b++)
                {
                    AddBassNote(bass, start + (b * bar), chord[0] - 24, 0.5, beat * 2.6);
                    AddBassNote(bass, start + (b * bar) + (beat * 2), chord[0] - 24, 0.3, beat * 1.6);
                }

                var steps = score.ArpEighths ? BarsPerChord * 8 : BarsPerChord * 4;
                var stepLength = length / steps;
                var pattern = new[] { 0, 1, 2, 3, 2, 1, 2, 3 };
                for (var s = 0; s < steps; s++)
                {
                    if (s > 0 && Synth.Pick(score.Seed, (c * 64) + s) < score.ArpRestChance)
                    {
                        continue;
                    }

                    var note = chord[pattern[s % pattern.Length] % chord.Length] + 12;
                    var accent = s % 4 == 0 ? 1.0 : 0.75;
                    AddPluck(arp, start + (s * stepLength), note, score.ArpLevel * accent);
                }

                if (score.Hats)
                {
                    for (var s = 0; s < BarsPerChord * 8; s++)
                    {
                        if (s % 2 == 1)
                        {
                            AddHat(hats, start + (s * bar / 8), 0.05, (c * 64) + s);
                        }
                    }
                }
            }

            Synth.LowPass(pad, score.PadCutoff);
            Synth.LowPass(pad, score.PadCutoff * 1.5);
            Synth.LowPass(bass, 400);
            Synth.LowPass(arp, 3200);
            Synth.Echo(arp, beat * 0.75, 0.4, 0.45);
            Synth.HighPass(hats, 5000);

            var mix = new double[total];
            for (var i = 0; i < total; i++)
            {
                mix[i] = pad[i] + bass[i] + arp[i] + hats[i];
            }

            var loop = Synth.WrapTail(mix, loopLength);
            return new SoundBuffer(KeyOf(piece), Synth.Normalise(loop, 0.7), Synth.Rate, loops: true);
        }

        public static string KeyOf(MusicPiece piece) => piece == MusicPiece.Night ? "music-night" : "music-day";

        private static void AddPadNote(double[] into, double start, double length, double note, double level, double phase)
        {
            const double attack = 1.4;
            const double release = 1.8;
            var hz = Synth.Hz(note);
            var first = Synth.Samples(start);
            var count = Synth.Samples(length + release);
            for (var i = 0; i < count && first + i < into.Length; i++)
            {
                var t = (double)i / Synth.Rate;
                var shimmer = 1.0 + (0.08 * Math.Sin(2 * Math.PI * 0.3 * t));
                into[first + i] += Synth.SoftSaw(phase + (hz * t)) * Synth.Swell(t, length, attack, release) * level * shimmer;
            }
        }

        private static void AddBassNote(double[] into, double start, double note, double level, double length)
        {
            var hz = Synth.Hz(note);
            var first = Synth.Samples(start);
            var count = Synth.Samples(length);
            for (var i = 0; i < count && first + i < into.Length; i++)
            {
                var t = (double)i / Synth.Rate;
                var tone = Synth.Sine(hz * t) + (0.25 * Synth.Sine(2 * hz * t));
                var fadeOut = 1.0 - Synth.Smooth((t - (length - 0.15)) / 0.15);
                into[first + i] += tone * Synth.Pluck(t, 0.012, 0.45) * fadeOut * level;
            }
        }

        private static void AddPluck(double[] into, double start, double note, double level)
        {
            var hz = Synth.Hz(note);
            var first = Synth.Samples(start);
            var count = Synth.Samples(1.4);
            for (var i = 0; i < count && first + i < into.Length; i++)
            {
                var t = (double)i / Synth.Rate;
                var tone = (0.7 * Synth.Sine(hz * t)) + (0.3 * Synth.Hollow(hz * t));
                into[first + i] += tone * Synth.Pluck(t, 0.004, 0.16) * level;
            }
        }

        private static void AddHat(double[] into, double start, double level, int seed)
        {
            var first = Synth.Samples(start);
            var count = Synth.Samples(0.08);
            for (var i = 0; i < count && first + i < into.Length; i++)
            {
                var t = (double)i / Synth.Rate;
                into[first + i] += Synth.Noise(seed, i) * Synth.Pluck(t, 0.001, 0.012) * level;
            }
        }
    }
}
