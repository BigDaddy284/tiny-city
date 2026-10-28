using System;
using System.Globalization;

namespace TinyTown.Art
{
    /// <summary>An 8-bit colour with alpha. The art is drawn with these and handed to Unity as raw pixels.</summary>
    public readonly struct Rgba : IEquatable<Rgba>
    {
        public static readonly Rgba Clear = new Rgba(0, 0, 0, 0);

        public Rgba(byte r, byte g, byte b, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public byte R { get; }

        public byte G { get; }

        public byte B { get; }

        public byte A { get; }

        public bool IsClear => A == 0;

        /// <summary>"#rrggbb" or "#rrggbbaa".</summary>
        public static Rgba Hex(string hex)
        {
            var s = hex.TrimStart('#');
            byte Part(int i) => byte.Parse(s.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return s.Length >= 8 ? new Rgba(Part(0), Part(1), Part(2), Part(3)) : new Rgba(Part(0), Part(1), Part(2));
        }

        /// <summary>Lighter above 1, darker below, keeping alpha.</summary>
        public Rgba Shade(double factor) => new Rgba(Clamp(R * factor), Clamp(G * factor), Clamp(B * factor), A);

        /// <summary><paramref name="t"/> of the way from this colour to <paramref name="other"/>.</summary>
        public Rgba Mix(Rgba other, double t) => new Rgba(
            Clamp(R + ((other.R - R) * t)),
            Clamp(G + ((other.G - G) * t)),
            Clamp(B + ((other.B - B) * t)),
            Clamp(A + ((other.A - A) * t)));

        public Rgba WithAlpha(byte a) => new Rgba(R, G, B, a);

        /// <summary>This colour painted over <paramref name="below"/>.</summary>
        public Rgba Over(Rgba below)
        {
            if (A == 255 || below.A == 0)
            {
                return this;
            }

            if (A == 0)
            {
                return below;
            }

            var a = A / 255.0;
            var outA = a + (below.A / 255.0 * (1 - a));
            byte C(byte top, byte bottom) => Clamp(((top * a) + (bottom * (below.A / 255.0) * (1 - a))) / outA);
            return new Rgba(C(R, below.R), C(G, below.G), C(B, below.B), Clamp(outA * 255));
        }

        public bool Equals(Rgba other) => R == other.R && G == other.G && B == other.B && A == other.A;

        public override bool Equals(object obj) => obj is Rgba other && Equals(other);

        public override int GetHashCode() => (R << 24) | (G << 16) | (B << 8) | A;

        public override string ToString() => $"#{R:x2}{G:x2}{B:x2}{A:x2}";

        private static byte Clamp(double v) => (byte)Math.Max(0, Math.Min(255, Math.Round(v)));
    }
}
