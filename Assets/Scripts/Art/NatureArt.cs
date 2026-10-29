using System;
using TinyCity.Simulation.Time;

namespace TinyCity.Art
{
    /// <summary>Trees for each season. Most are broadleaf trees that blossom, turn and go bare; some are evergreen pines.</summary>
    public static class NatureArt
    {
        public const int TreeWidth = 72;
        public const int TreeHeight = 104;

        /// <summary>Whether the tree at a map position is a pine rather than a broadleaf tree.</summary>
        public static bool IsPine(int variant) => variant % 10 < 3;

        /// <summary>A tree standing on one tile. Its anchor is the centre of that tile.</summary>
        public static PixelCanvas Tree(Palette p, int variant)
        {
            var canvas = new PixelCanvas(TreeWidth, TreeHeight, TreeWidth / 2.0, TreeHeight - 16);
            var cx = canvas.AnchorX + ((Noise.Value(variant, 1) - 0.5) * 6);
            var baseY = canvas.AnchorY;
            if (IsPine(variant))
            {
                Pine(canvas, p, cx, baseY, variant);
            }
            else
            {
                Broadleaf(canvas, p, cx, baseY, variant);
            }

            canvas.Ellipse(cx + 4, baseY + 1, 22, 9, Palette.Shadow, under: true);

            return canvas;
        }

        private static void Broadleaf(PixelCanvas canvas, Palette p, double cx, double baseY, int variant)
        {
            var size = 0.85 + (Noise.Value(variant, 2) * 0.3);
            var trunkTop = baseY - (30 * size);
            Trunk(canvas, cx, baseY, trunkTop, 3.5);
            if (p.Season == Season.Winter)
            {
                Branches(canvas, p, cx, trunkTop, size, variant);
                canvas.Outline(Palette.Outline);
                return;
            }

            // A cluster of overlapping blobs, drawn dark first then lit from the upper left.
            var crownY = trunkTop - (14 * size);
            var blobs = new (double x, double y, double r)[]
            {
                (0, 0, 19), (-13, 6, 13), (13, 5, 13), (-7, -11, 13), (8, -12, 12), (0, 9, 13),
            };
            foreach (var (x, y, r) in blobs)
            {
                canvas.Disc(cx + (x * size) + 1, crownY + (y * size) + 2, r * size, p.LeavesDark);
            }

            foreach (var (x, y, r) in blobs)
            {
                canvas.Disc(cx + (x * size) - 1, crownY + (y * size) - 1, (r - 2.5) * size, p.Leaves);
            }

            foreach (var (x, y, r) in blobs)
            {
                canvas.Disc(cx + (x * size) - 4, crownY + (y * size) - 5, (r - 8) * size, p.LeavesLight);
            }

            Speckle(canvas, p, variant);
            canvas.Outline(Palette.Outline);

            // Blossom in spring, fruit in summer, a few falling leaves in autumn.
            var dots = p.Season == Season.Spring ? 22 : p.Season == Season.Summer ? 5 : 8;
            for (var i = 0; i < dots; i++)
            {
                var a = Noise.Value(variant, i, 40) * Math.PI * 2;
                var d = Math.Sqrt(Noise.Value(variant, i, 41)) * 18 * size;
                var x = (int)(cx + (Math.Cos(a) * d));
                var y = (int)(crownY + (Math.Sin(a) * d * 0.9));
                var colour = p.Season == Season.Autumn ? p.Accent : p.Season == Season.Spring ? (i % 3 == 0 ? Palette.White : p.Accent) : p.Accent;
                canvas.FillRect(x, y, 2, 2, colour);
            }

            if (p.Season == Season.Autumn)
            {
                for (var i = 0; i < 6; i++)
                {
                    var x = (int)(cx + ((Noise.Value(variant, i, 50) - 0.5) * 40));
                    var y = (int)(baseY - 2 + ((Noise.Value(variant, i, 51) - 0.5) * 12));
                    canvas.FillRect(x, y, 2, 1, i % 2 == 0 ? p.Leaves : p.LeavesLight);
                }
            }
        }

        private static void Pine(PixelCanvas canvas, Palette p, double cx, double baseY, int variant)
        {
            var size = 0.85 + (Noise.Value(variant, 3) * 0.3);
            Trunk(canvas, cx, baseY, baseY - 16, 3);
            for (var tier = 0; tier < 4; tier++)
            {
                var bottom = baseY - 10 - (tier * 17 * size);
                var half = (24 - (tier * 5)) * size;
                var top = bottom - (26 * size);
                for (var y = (int)top; y <= (int)bottom; y++)
                {
                    var f = (y - top) / (bottom - top);
                    var w = half * f;
                    for (var x = (int)(cx - w); x <= (int)(cx + w); x++)
                    {
                        var lit = x < cx - (w * 0.25);
                        var colour = lit ? p.PineLeaves.Shade(1.12) : x > cx + (w * 0.4) ? p.PineLeavesDark : p.PineLeaves;
                        if (p.IsWinter && (y - top) < 5 + (Noise.Value(x, tier, variant) * 4))
                        {
                            colour = x > cx + (w * 0.4) ? Palette.SnowShade : Palette.Snow;
                        }
                        else if (y > bottom - 2 && Noise.Value(x, tier, variant + 1) < 0.5)
                        {
                            colour = p.PineLeavesDark;
                        }

                        canvas.Paint(x, y, colour);
                    }
                }
            }

            canvas.Outline(Palette.Outline);
        }

        private static void Trunk(PixelCanvas canvas, double cx, double baseY, double topY, double halfWidth)
        {
            for (var y = (int)topY; y <= (int)baseY; y++)
            {
                var flare = y > baseY - 4 ? 1.5 : 0;
                for (var x = (int)(cx - halfWidth - flare); x <= (int)(cx + halfWidth + flare); x++)
                {
                    canvas.Paint(x, y, x < cx - 1 ? Palette.Wood : Palette.WoodDark);
                }
            }
        }

        private static void Branches(PixelCanvas canvas, Palette p, double cx, double trunkTop, double size, int variant)
        {
            // Bare twigs with snow along their tops.
            for (var i = 0; i < 7; i++)
            {
                var angle = -Math.PI / 2 + ((i - 3) * 0.42) + ((Noise.Value(variant, i, 60) - 0.5) * 0.3);
                var length = (22 + (Noise.Value(variant, i, 61) * 12)) * size;
                var startY = trunkTop + 6 - (i % 3 * 4);
                for (var s = 0.0; s < length; s += 0.7)
                {
                    var x = cx + (Math.Cos(angle) * s);
                    var y = startY + (Math.Sin(angle) * s);
                    var thick = s < length * 0.4 ? 2 : 1;
                    canvas.FillRect((int)x, (int)y, thick, thick, p.Leaves);
                    if (Noise.Value((int)x, (int)y, variant) < 0.35)
                    {
                        canvas.Paint((int)x, (int)y - 1, Palette.Snow);
                    }
                }
            }

            canvas.FillRect((int)cx - 3, (int)trunkTop - 4, 6, 8, p.Leaves);
        }

        private static void Speckle(PixelCanvas canvas, Palette p, int variant)
        {
            for (var y = 0; y < canvas.Height; y++)
            {
                for (var x = 0; x < canvas.Width; x++)
                {
                    var c = canvas.Get(x, y);
                    if (c.Equals(p.Leaves) && Noise.Value(x >> 1, y, variant) < 0.12)
                    {
                        canvas.Set(x, y, p.LeavesDark.Mix(p.Leaves, 0.5));
                    }
                    else if (c.Equals(p.LeavesLight) && Noise.Value(x >> 1, y, variant + 1) < 0.15)
                    {
                        canvas.Set(x, y, p.Leaves);
                    }
                }
            }
        }
    }
}
