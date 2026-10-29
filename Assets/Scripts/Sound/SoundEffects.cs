using System;

namespace TinyCity.Sound
{
    /// <summary>Short sounds for what residents do.</summary>
    public enum SoundEffect
    {
        Footstep,
        Door,
        Typing,
        Till,
        Cooking,
        Drink,
        ChessMove,
        Piano,
        Splash,
        Digging,
        Chime,
    }

    /// <summary>Each effect is synthesised from noise and tones. Footsteps have several variations so a walk doesn't sound mechanical.</summary>
    public static class SoundEffects
    {
        public const int FootstepVariants = 4;

        public static int VariantsOf(SoundEffect effect) => effect == SoundEffect.Footstep ? FootstepVariants : 1;

        public static string KeyOf(SoundEffect effect, int variant) =>
            VariantsOf(effect) > 1 ? $"sfx-{Name(effect)}-{variant % VariantsOf(effect)}" : $"sfx-{Name(effect)}";

        public static string Name(SoundEffect effect)
        {
            switch (effect)
            {
                case SoundEffect.ChessMove: return "chess";
                default: return effect.ToString().ToLowerInvariant();
            }
        }

        public static SoundBuffer Make(SoundEffect effect, int variant = 0)
        {
            variant %= VariantsOf(effect);
            double[] samples;
            double peak;
            switch (effect)
            {
                case SoundEffect.Footstep:
                    samples = Footstep(variant);
                    peak = 0.5;
                    break;
                case SoundEffect.Door:
                    samples = Door();
                    peak = 0.55;
                    break;
                case SoundEffect.Typing:
                    samples = Typing();
                    peak = 0.35;
                    break;
                case SoundEffect.Till:
                    samples = Till();
                    peak = 0.35;
                    break;
                case SoundEffect.Cooking:
                    samples = Cooking();
                    peak = 0.3;
                    break;
                case SoundEffect.Drink:
                    samples = Drink();
                    peak = 0.4;
                    break;
                case SoundEffect.ChessMove:
                    samples = ChessMove();
                    peak = 0.4;
                    break;
                case SoundEffect.Piano:
                    samples = Piano();
                    peak = 0.45;
                    break;
                case SoundEffect.Splash:
                    samples = Splash();
                    peak = 0.4;
                    break;
                case SoundEffect.Digging:
                    samples = Digging();
                    peak = 0.35;
                    break;
                default:
                    samples = Chime();
                    peak = 0.45;
                    break;
            }

            return new SoundBuffer(KeyOf(effect, variant), Synth.Normalise(samples, peak), Synth.Rate, loops: false);
        }

        /// <summary>A soft heel on paving: a muffled tap and a low thump.</summary>
        private static double[] Footstep(int variant)
        {
            var s = new double[Synth.Samples(0.12)];
            var thump = 80 + (variant * 9);
            for (var i = 0; i < s.Length; i++)
            {
                var t = (double)i / Synth.Rate;
                s[i] = (Synth.Noise(300 + variant, i) * Synth.Pluck(t, 0.002, 0.012)) + (0.8 * Synth.Sine(thump * t) * Synth.Pluck(t, 0.003, 0.02));
            }

            Synth.LowPass(s, 700 + (variant * 120));
            Synth.LowPass(s, 900 + (variant * 120));
            return s;
        }

        /// <summary>A sliding door: an airy whoosh that opens and closes, and a two-note chime.</summary>
        private static double[] Door()
        {
            var length = 0.9;
            var s = new double[Synth.Samples(length)];
            var low = 0.0;
            var smooth = 0.0;
            for (var i = 0; i < s.Length; i++)
            {
                var t = (double)i / Synth.Rate;
                var cutoff = 300 + (1400 * Synth.Bell(t, 0.6));
                low += Synth.LowPassFactor(cutoff) * (Synth.Noise(310, i) - low);
                smooth += Synth.LowPassFactor(cutoff) * (low - smooth);
                s[i] = smooth * Synth.Bell(t, 0.6) * 2.5;
                s[i] += 0.25 * Synth.Sine(Synth.Hz(88) * t) * Synth.Pluck(t - 0.05, 0.005, 0.12);
                s[i] += 0.2 * Synth.Sine(Synth.Hz(95) * t) * Synth.Pluck(t - 0.2, 0.005, 0.15);
            }

            return s;
        }

        /// <summary>A few soft key taps.</summary>
        private static double[] Typing()
        {
            var s = new double[Synth.Samples(0.8)];
            for (var k = 0; k < 6; k++)
            {
                var start = Synth.Samples((k * 0.11) + (Synth.Pick(320, k) * 0.05));
                for (var i = 0; i < Synth.Samples(0.03) && start + i < s.Length; i++)
                {
                    var t = (double)i / Synth.Rate;
                    s[start + i] += Synth.Noise(321 + k, i) * Synth.Pluck(t, 0.0005, 0.004);
                }
            }

            Synth.HighPass(s, 1500);
            return s;
        }

        /// <summary>The shop's two beeps as something is rung up.</summary>
        private static double[] Till()
        {
            var s = new double[Synth.Samples(0.4)];
            foreach (var (start, note) in new[] { (0.0, 88.0), (0.14, 93.0) })
            {
                var hz = Synth.Hz(note);
                var first = Synth.Samples(start);
                for (var i = 0; i < Synth.Samples(0.12) && first + i < s.Length; i++)
                {
                    var t = (double)i / Synth.Rate;
                    s[first + i] += Synth.Hollow(hz * t) * Synth.Swell(t, 0.08, 0.005, 0.03);
                }
            }

            Synth.LowPass(s, 3000);
            return s;
        }

        /// <summary>A pan sizzling: hiss with crackles.</summary>
        private static double[] Cooking()
        {
            var length = 1.6;
            var s = new double[Synth.Samples(length)];
            for (var i = 0; i < s.Length; i++)
            {
                var t = (double)i / Synth.Rate;
                var crackle = Synth.Pick(330, i) < 0.002 ? 3.0 : 0.0;
                s[i] = ((Synth.Noise(331, i) * 0.5) + (crackle * Synth.Noise(332, i))) * Synth.Swell(t, length - 0.5, 0.2, 0.5);
            }

            Synth.HighPass(s, 2000);
            Synth.LowPass(s, 7000);
            return s;
        }

        /// <summary>Pouring and a sip: little rising bubbles.</summary>
        private static double[] Drink()
        {
            var s = new double[Synth.Samples(0.5)];
            for (var b = 0; b < 4; b++)
            {
                var first = Synth.Samples(b * 0.1);
                var count = Synth.Samples(0.07);
                for (var i = 0; i < count && first + i < s.Length; i++)
                {
                    var t = (double)i / Synth.Rate;
                    var hz = 320 + (b * 40) + (4000 * t);
                    s[first + i] += Synth.Sine(hz * t) * Synth.Bell(t, 0.07);
                }
            }

            return s;
        }

        /// <summary>A wooden piece set down on the board.</summary>
        private static double[] ChessMove()
        {
            var s = new double[Synth.Samples(0.15)];
            for (var i = 0; i < s.Length; i++)
            {
                var t = (double)i / Synth.Rate;
                s[i] = (Synth.Sine(820 * t) * Synth.Pluck(t, 0.001, 0.015)) + (0.5 * Synth.Noise(340, i) * Synth.Pluck(t, 0.0005, 0.004));
            }

            Synth.LowPass(s, 4000);
            return s;
        }

        /// <summary>A short phrase on an electric piano.</summary>
        private static double[] Piano()
        {
            var notes = new[] { 72.0, 76.0, 79.0, 84.0, 79.0 };
            var s = new double[Synth.Samples(2.4)];
            for (var n = 0; n < notes.Length; n++)
            {
                var hz = Synth.Hz(notes[n]);
                var first = Synth.Samples(n * 0.28);
                for (var i = 0; first + i < s.Length; i++)
                {
                    var t = (double)i / Synth.Rate;
                    var tone = Synth.Sine(hz * t) + (0.3 * Synth.Sine(2 * hz * t) * Synth.Pluck(t, 0.001, 0.08)) + (0.1 * Synth.Sine(3 * hz * t));
                    s[first + i] += tone * Synth.Pluck(t, 0.003, 0.35);
                }
            }

            return s;
        }

        /// <summary>A line cast into the water.</summary>
        private static double[] Splash()
        {
            var s = new double[Synth.Samples(0.6)];
            for (var i = 0; i < s.Length; i++)
            {
                var t = (double)i / Synth.Rate;
                s[i] = (Synth.Noise(350, i) * Synth.Pluck(t, 0.004, 0.08)) + (0.3 * Synth.Sine((500 - (500 * t)) * t) * Synth.Pluck(t, 0.01, 0.06));
            }

            Synth.LowPass(s, 2500);
            return s;
        }

        /// <summary>A trowel in soil: two soft scrapes.</summary>
        private static double[] Digging()
        {
            var s = new double[Synth.Samples(0.7)];
            for (var i = 0; i < s.Length; i++)
            {
                var t = (double)i / Synth.Rate;
                s[i] = Synth.Noise(360, i) * (Synth.Bell(t, 0.25) + Synth.Bell(t - 0.35, 0.25));
            }

            Synth.LowPass(s, 1200);
            Synth.HighPass(s, 200);
            return s;
        }

        /// <summary>A bright three-note chime for big moments in the city news.</summary>
        private static double[] Chime()
        {
            var s = new double[Synth.Samples(1.6)];
            var notes = new[] { 84.0, 88.0, 91.0 };
            for (var n = 0; n < notes.Length; n++)
            {
                var hz = Synth.Hz(notes[n]);
                var first = Synth.Samples(n * 0.12);
                for (var i = 0; first + i < s.Length; i++)
                {
                    var t = (double)i / Synth.Rate;
                    s[first + i] += (Synth.Sine(hz * t) + (0.2 * Synth.Sine(2 * hz * t))) * Synth.Pluck(t, 0.004, 0.25);
                }
            }

            return s;
        }
    }
}
