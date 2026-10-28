using System.Collections.Generic;
using TinyTown.Simulation.Residents;

namespace TinyTown.Art
{
    /// <summary>
    /// The little thought bubbles over residents' heads. Each is a cream bubble with a tail, outlined like everything
    /// else, holding a 7 x 5 symbol. The anchor is the tip of the tail, which goes just above the head.
    /// </summary>
    public static class MoodArt
    {
        public const int Width = 13;
        public const int Height = 12;

        // Symbols, 7 wide and 5 tall: each letter is a colour in the key below, '.' is the bubble.
        private static readonly Dictionary<Mood, string[]> Symbols = new Dictionary<Mood, string[]>
        {
            [Mood.Happy] = new[] { ".......", "..m.m..", ".......", ".m...m.", "..mmm.." },
            [Mood.Asleep] = new[] { "....zzz", ".....z.", "zzz.zzz", ".z.....", "zzz...." },
            [Mood.Ill] = new[] { ".qqqqq.", "qq.q.qq", "qqqqqqq", "qq.q.qq", ".qqqqq." },
            [Mood.NeedsToilet] = new[] { "...r...", "...r...", "...r...", ".......", "...r..." },
            [Mood.Hungry] = new[] { ".......", "..bbb..", ".bbbbb.", ".bwbwb.", ".bbbbb." },
            [Mood.Thirsty] = new[] { "...s...", "..sss..", ".sssss.", ".sswss.", "..sss.." },
            [Mood.Tired] = new[] { ".......", ".ee.ee.", ".......", "..eee..", "......." },
            [Mood.Grubby] = new[] { ".g..g..", "g..g..g", ".g..g..", "g..g..g", ".g..g.." },
            [Mood.Lonely] = new[] { "..ccc..", ".ccccc.", "ccccccc", ".s.s.s.", "s.s.s.." },
            [Mood.Bored] = new[] { ".......", ".......", "e..e..e", ".......", "......." },
            [Mood.InLove] = new[] { ".hh.hh.", "hhhhhhh", "hhhhhhh", ".hhhhh.", "...h..." },
        };

        private static Rgba Key(char c)
        {
            switch (c)
            {
                case 'm': return Palette.Marigold.Shade(0.8);
                case 'z': return Palette.Sleepy;
                case 'q': return Palette.Queasy;
                case 'r': return Palette.SignRed;
                case 'b': return Palette.Bread;
                case 'w': return Palette.WoodDark;
                case 's': return Palette.Sky.Shade(0.85);
                case 'e': return Palette.Slate;
                case 'g': return Palette.Moss;
                case 'c': return Palette.SlateLight;
                case 'h': return Palette.Heart;
                default: return Palette.Bubble;
            }
        }

        /// <summary>Every mood that has an icon.</summary>
        public static IEnumerable<Mood> Shown => Symbols.Keys;

        /// <summary>The icon for a mood, or null for <see cref="Mood.None"/>.</summary>
        public static PixelCanvas Icon(Mood mood)
        {
            if (!Symbols.TryGetValue(mood, out var symbol))
            {
                return null;
            }

            var canvas = new PixelCanvas(Width, Height, Width / 2.0, Height);

            // Bubble with rounded corners and a tail pointing down at the head.
            canvas.FillRect(1, 1, Width - 2, 8, Palette.Bubble);
            canvas.FillRect(2, 0, Width - 4, 10, Palette.Bubble);
            canvas.FillRect(5, 10, 3, 1, Palette.Bubble);
            canvas.Set(6, 11, Palette.Bubble);
            for (var y = 0; y < symbol.Length; y++)
            {
                for (var x = 0; x < symbol[y].Length; x++)
                {
                    if (symbol[y][x] != '.')
                    {
                        canvas.Set(3 + x, 2 + y, Key(symbol[y][x]));
                    }
                }
            }

            canvas.Outline(Palette.Outline);
            return canvas;
        }
    }
}
