using System;

namespace TinyTown.Art
{
    /// <summary>Repeatable pseudo-random numbers from coordinates, so the same town always looks the same.</summary>
    public static class Noise
    {
        public static uint Hash(int a, int b, int c = 0)
        {
            unchecked
            {
                var h = 2166136261u;
                h = (h ^ (uint)a) * 16777619u;
                h = (h ^ (uint)b) * 16777619u;
                h = (h ^ (uint)c) * 16777619u;
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                return h;
            }
        }

        /// <summary>A number in [0, 1).</summary>
        public static double Value(int a, int b, int c = 0) => Hash(a, b, c) / 4294967296.0;

        /// <summary>A stable number for a text id, such as a map id.</summary>
        public static int Of(string text)
        {
            unchecked
            {
                var h = 2166136261u;
                foreach (var ch in text ?? string.Empty)
                {
                    h = (h ^ ch) * 16777619u;
                }

                return (int)(h & 0x7fffffff);
            }
        }

        /// <summary>Smooth value noise in [0, 1), with features about one unit apart.</summary>
        public static double Smooth(double x, double y, int seed)
        {
            var x0 = (int)Math.Floor(x);
            var y0 = (int)Math.Floor(y);
            var fx = x - x0;
            var fy = y - y0;
            fx = fx * fx * (3 - (2 * fx));
            fy = fy * fy * (3 - (2 * fy));
            var a = Value(x0, y0, seed);
            var b = Value(x0 + 1, y0, seed);
            var c = Value(x0, y0 + 1, seed);
            var d = Value(x0 + 1, y0 + 1, seed);
            return a + ((b - a) * fx) + ((c - a) * fy) + ((a - b - c + d) * fx * fy);
        }
    }
}
