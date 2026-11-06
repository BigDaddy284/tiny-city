using System;
using TinyCity.Simulation.Time;
using TinyCity.Simulation.World;

namespace TinyCity.Art
{
    /// <summary>Furniture and outdoor fittings, one picture per <see cref="ObjectType"/>, each standing on its tile.</summary>
    public static class FurnitureArt
    {
        public const int CanvasWidth = 72;
        public const int CanvasHeight = 104;

        /// <summary>A new canvas whose anchor is the centre of the object's tile.</summary>
        public static PixelCanvas NewCanvas() => new PixelCanvas(CanvasWidth, CanvasHeight, CanvasWidth / 2.0, CanvasHeight - 16);

        public static PixelCanvas Render(ObjectType type, int variant, Palette palette, bool outdoors)
        {
            var canvas = NewCanvas();
            var p = new IsoPainter(canvas);
            var accent = Palette.Accents[variant % Palette.Accents.Length];
            switch (type)
            {
                case ObjectType.Bed when outdoors:
                    // A camp bed (Tiny City 2's starter camp): a mat and bedroll under an awning on two poles.
                    p.Box(0.04, 0.06, 0, 0.92, 0.88, 2, Palette.Ebony);
                    p.Box(0.18, 0.2, 2, 0.66, 0.66, 5, accent);
                    p.Top(0.18, 0.2, 7, 0.66, 0.66, (s, t) => ((int)(s * 4) + (int)(t * 4)) % 2 == 0 ? accent : accent.Mix(Palette.White, 0.35));
                    p.Box(0.22, 0.22, 7, 0.16, 0.6, 3, Palette.White);
                    p.Post(0.08, 0.1, 0, 0.03, 30, Palette.WoodDark);
                    p.Post(0.08, 0.9, 0, 0.03, 30, Palette.WoodDark);
                    p.Box(0.02, 0.04, 28, 0.36, 0.92, 3, accent.Shade(0.7));
                    break;
                case ObjectType.Bed:
                    p.Box(0.08, 0.12, 0, 0.84, 0.76, 9, Palette.Wood);
                    p.Box(0.04, 0.1, 0, 0.12, 0.8, 24, Palette.WoodDark);
                    p.Box(0.16, 0.16, 9, 0.74, 0.68, 4, Palette.White);
                    p.Box(0.2, 0.22, 13, 0.18, 0.56, 4, Palette.White.Shade(1.02));
                    p.Box(0.42, 0.14, 9, 0.52, 0.72, 6, accent);
                    p.Top(0.42, 0.14, 15, 0.52, 0.72, (s, t) => ((int)(s * 4) + (int)(t * 4)) % 2 == 0 ? accent : accent.Mix(Palette.White, 0.45));
                    break;
                case ObjectType.Kitchen:
                    p.Box(0.08, 0.2, 0, 0.84, 0.7, 20, Palette.White);
                    p.LeftWall(0.2, 0.9, 3, 0.6, 12, (s, t) => s < 0.05 || s > 0.95 || t < 0.08 || t > 0.92 ? Palette.Metal : Palette.Iron);
                    p.Top(0.08, 0.2, 20, 0.84, 0.7, (s, t) =>
                    {
                        var a = Math.Pow((s - 0.3) * 3, 2) + Math.Pow((t - 0.5) * 3, 2);
                        var b = Math.Pow((s - 0.7) * 3, 2) + Math.Pow((t - 0.5) * 3, 2);
                        return a < 0.35 || b < 0.35 ? Palette.Charcoal : Palette.Stone.Shade(1.1);
                    });
                    p.Box(0.55, 0.22, 20, 0.12, 0.12, 9, Palette.Tomato);
                    break;
                case ObjectType.GardenPlot:
                    p.Box(0.05, 0.05, 0, 0.9, 0.06, 5, Palette.Wood);
                    p.Box(0.05, 0.89, 0, 0.9, 0.06, 5, Palette.Wood);
                    for (var i = 0; i < 3; i++)
                    {
                        Plant(p, palette, 0.25 + (i * 0.25), 0.5, variant + i);
                    }

                    break;
                case ObjectType.Bench:
                    foreach (var leg in new[] { 0.14, 0.78 })
                    {
                        p.Box(leg, 0.35, 0, 0.08, 0.36, 9, Palette.WoodDark);
                    }

                    p.Box(0.1, 0.32, 9, 0.8, 0.4, 3, Palette.Wood);
                    p.Box(0.1, 0.26, 9, 0.8, 0.07, 16, Palette.Wood);
                    p.Top(0.1, 0.32, 12, 0.8, 0.4, (s, t) => (int)(t * 3) % 2 == 0 ? Palette.WoodLight : Palette.Wood);
                    SnowOn(p, palette, outdoors, 0.1, 0.32, 12, 0.8, 0.4);
                    break;
                case ObjectType.Workbench:
                    foreach (var (lu, lv) in new[] { (0.1, 0.25), (0.82, 0.25), (0.1, 0.78), (0.82, 0.78) })
                    {
                        p.Box(lu, lv, 0, 0.08, 0.08, 18, Palette.WoodDark);
                    }

                    p.Box(0.06, 0.2, 18, 0.88, 0.68, 4, Palette.Wood);
                    p.Box(0.15, 0.25, 22, 0.16, 0.14, 6, Palette.Metal);
                    p.Box(0.5, 0.5, 22, 0.3, 0.05, 2, Palette.Tomato);
                    p.Box(0.22, 0.25, 4, 0.6, 0.55, 2, Palette.WoodDark);
                    break;
                case ObjectType.FishingSpot:
                    p.Box(0.05, 0.2, 0, 0.9, 0.6, 4, Palette.Wood);
                    p.Top(0.05, 0.2, 4, 0.9, 0.6, (s, t) => (int)(s * 6) % 2 == 0 ? Palette.WoodLight : Palette.Wood);
                    p.Post(0.1, 0.25, -6, 0.04, 14, Palette.WoodDark);
                    p.Post(0.1, 0.75, -6, 0.04, 14, Palette.WoodDark);
                    p.Box(0.7, 0.3, 4, 0.18, 0.12, 6, Palette.Moss);
                    SnowOn(p, palette, outdoors, 0.05, 0.2, 4, 0.9, 0.6);
                    break;
                case ObjectType.GatheringSpot:
                    Maypole(p, palette);
                    break;
                case ObjectType.MarketStall:
                {
                    // A wooden stall under a striped awning, its counter laid out with goods.
                    var stripe = Palette.Bunting[variant % Palette.Bunting.Length];
                    foreach (var (su, sv) in new[] { (0.12, 0.3), (0.84, 0.3), (0.12, 0.72), (0.84, 0.72) })
                    {
                        p.Post(su, sv, 0, 0.03, 40, Palette.WoodDark);
                    }

                    p.Box(0.1, 0.4, 0, 0.8, 0.36, 16, Palette.Wood);
                    p.Top(0.12, 0.42, 16, 0.76, 0.32, (s, t) => Noise.Value((int)(s * 6), (int)(t * 3), variant) < 0.7 ? Palette.Goods[((int)(s * 6) + variant) % Palette.Goods.Length] : Palette.WoodLight);
                    p.Top(0.04, 0.24, 40, 0.92, 0.6, (s, t) => (int)(s * 6) % 2 == 0 ? stripe : Palette.White);
                    SnowOn(p, palette, outdoors, 0.04, 0.24, 40.5, 0.92, 0.6);
                    break;
                }

                case ObjectType.Woodland:
                    // Tiny City 2's woodland: a young tree, two stumps and a pile of cut logs.
                    foreach (var (tu, tv, tall) in new[] { (0.22, 0.22, 1.0), (0.5, 0.18, 0.75) })
                    {
                        p.Post(tu, tv, 0, 0.04, 10 * tall, Palette.WoodDark);
                        for (var tier = 0; tier < 3; tier++)
                        {
                            var leaf = palette.IsWinter && tier == 2 ? Palette.Snow : tier % 2 == 0 ? palette.PineLeavesDark : palette.PineLeaves;
                            p.Post(tu, tv, (8 + (tier * 8)) * tall, (0.2 - (tier * 0.055)) * tall, 9 * tall, leaf);
                        }
                    }

                    foreach (var (su, sv) in new[] { (0.72, 0.26), (0.24, 0.72) })
                    {
                        p.Post(su, sv, 0, 0.11, 7, Palette.Wood);
                        p.FlatDisc(su, sv, 7, 0.09, Palette.WoodLight);
                    }

                    for (var i = 0; i < 3; i++)
                    {
                        p.Box(0.48 + (i * 0.13), 0.5, 0, 0.11, 0.38, 6, Palette.WoodLight, Palette.Wood, Palette.WoodDark);
                    }

                    p.Box(0.54, 0.5, 6, 0.11, 0.38, 6, Palette.WoodLight, Palette.Wood, Palette.WoodDark);
                    p.Box(0.67, 0.5, 6, 0.11, 0.38, 6, Palette.WoodLight, Palette.Wood, Palette.WoodDark);
                    SnowOn(p, palette, outdoors, 0.54, 0.5, 12, 0.24, 0.38);
                    break;
                case ObjectType.RockPile:
                    // Tiny City 2's rock pile: rough boulders, the top one cracked open.
                    foreach (var (ru, rv, rz, du, dv, dz) in new[] { (0.14, 0.3, 0.0, 0.42, 0.5, 14.0), (0.5, 0.2, 0.0, 0.36, 0.4, 10.0), (0.46, 0.6, 0.0, 0.34, 0.3, 8.0), (0.24, 0.36, 14.0, 0.26, 0.3, 9.0) })
                    {
                        p.Box(ru, rv, rz, du, dv, dz, Palette.Metal, Palette.Stone, Palette.StoneDark);
                        p.Top(ru, rv, rz + dz, du, dv, (a, b) => Noise.Value((int)(a * 5), (int)(b * 5), variant + (int)(ru * 10)) < 0.3 ? Palette.Stone : Palette.Metal);
                    }

                    p.Box(0.3, 0.44, 23, 0.1, 0.03, 0.5, Palette.StoneDark);
                    SnowOn(p, palette, outdoors, 0.24, 0.36, 23, 0.26, 0.3);
                    break;
                case ObjectType.StickPile:
                    // Tiny City 2's fallen branches: a loose heap of thin sticks lying every which way.
                    foreach (var (bu, bv, du, dv, bz) in new[] { (0.2, 0.42, 0.6, 0.07, 0.0), (0.42, 0.2, 0.07, 0.6, 0.0), (0.26, 0.6, 0.5, 0.06, 0.0), (0.3, 0.34, 0.45, 0.06, 3.0), (0.56, 0.3, 0.06, 0.42, 3.0), (0.36, 0.48, 0.38, 0.05, 6.0) })
                    {
                        p.Box(bu, bv, bz, du, dv, 3, Palette.WoodLight, Palette.Wood, Palette.WoodDark);
                    }

                    p.Post(0.66, 0.66, 0, 0.05, 4, Palette.LeafGreen);
                    SnowOn(p, palette, outdoors, 0.36, 0.48, 9, 0.38, 0.05);
                    break;
                case ObjectType.LooseStones:
                    // Tiny City 2's loose stones: a scatter of small stones on the ground, small enough to carry.
                    foreach (var (su, sv, sd, sz) in new[] { (0.2, 0.3, 0.16, 5.0), (0.46, 0.22, 0.12, 4.0), (0.62, 0.46, 0.18, 6.0), (0.3, 0.6, 0.14, 4.0), (0.5, 0.66, 0.1, 3.0) })
                    {
                        p.Box(su, sv, 0, sd, sd, sz, Palette.Metal, Palette.Stone, Palette.StoneDark);
                    }

                    SnowOn(p, palette, outdoors, 0.62, 0.46, 6, 0.18, 0.18);
                    break;
                case ObjectType.SleepingSpot:
                    // Tiny City 2's sleeping spot: a coat spread on the bare ground with a rolled-up bundle for a pillow.
                    p.Box(0.16, 0.14, 0, 0.7, 0.72, 1.5, accent.Shade(0.8));
                    p.Top(0.16, 0.14, 1.5, 0.7, 0.72, (s, t) => s < 0.06 || s > 0.94 || t < 0.06 || t > 0.94 ? accent.Shade(0.6) : accent.Shade(0.8));
                    p.Box(0.2, 0.22, 1.5, 0.16, 0.56, 4, Palette.Ebony.Mix(Palette.White, 0.25));
                    break;
                case ObjectType.BuildPlot:
                    // Tiny City 2's plot marker: a post with a sign board and a neon strip, and two corner stakes.
                    p.Post(0.5, 0.3, 0, 0.04, 22, Palette.WoodDark);
                    p.Box(0.3, 0.26, 18, 0.4, 0.06, 12, Palette.Slate);
                    p.Box(0.34, 0.25, 23, 0.32, 0.02, 1.5, Palette.NeonMint);
                    p.Post(0.1, 0.1, 0, 0.03, 8, Palette.Wood);
                    p.Post(0.9, 0.1, 0, 0.03, 8, Palette.Wood);
                    p.Box(0.1, 0.09, 6, 0.8, 0.02, 0.8, Palette.NeonMint);
                    SnowOn(p, palette, outdoors, 0.3, 0.26, 30, 0.4, 0.06);
                    break;
                default:
                    p.Box(0.3, 0.3, 0, 0.4, 0.4, 12, Palette.Stone);
                    break;
            }

            canvas.Outline(Palette.Outline);
            if (outdoors)
            {
                canvas.Ellipse(canvas.AnchorX + 3, canvas.AnchorY + 1, 20, 8, Palette.Shadow, under: true);
            }

            return canvas;
        }

        /// <summary>A pool table, or the bigger snooker table, with a couple of balls on the cloth.</summary>
        private static void CueTable(IsoPainter p, bool snooker)
        {
            var (u, v, du, dv) = snooker ? (0.02, 0.12, 0.96, 0.76) : (0.1, 0.2, 0.8, 0.6);
            foreach (var (lu, lv) in new[] { (u + 0.04, v + 0.04), (u + du - 0.1, v + 0.04), (u + 0.04, v + dv - 0.1), (u + du - 0.1, v + dv - 0.1) })
            {
                p.Box(lu, lv, 0, 0.06, 0.06, 13, Palette.WoodDark);
            }

            p.Box(u, v, 13, du, dv, 4, Palette.Wood);
            p.Top(u + 0.05, v + 0.05, 17, du - 0.1, dv - 0.1, (s, t) => snooker ? Palette.Moss : Palette.Teal);
            var balls = snooker
                ? new[] { (0.3, 0.45, Palette.SignRed), (0.34, 0.5, Palette.SignRed), (0.3, 0.55, Palette.SignRed), (0.7, 0.5, Palette.White), (0.55, 0.35, Palette.Ebony) }
                : new[] { (0.35, 0.45, Palette.Yellow), (0.4, 0.55, Palette.SignRed), (0.65, 0.5, Palette.White) };
            foreach (var (bu, bv, colour) in balls)
            {
                p.Post(bu, bv, 17, 0.03, 2, colour);
            }
        }

        private static void Plant(IsoPainter p, Palette palette, double u, double v, int variant)
        {
            if (palette.Season == Season.Winter)
            {
                p.Post(u, v, 0, 0.08, 2, Palette.Snow);
                return;
            }

            var height = palette.Season == Season.Spring ? 5 : 9;
            p.Post(u, v, 0, 0.1, height, palette.LeavesDark);
            p.Post(u - 0.03, v - 0.03, height - 3, 0.07, 3, palette.Leaves);
            if (palette.Season == Season.Summer && variant % 2 == 0)
            {
                p.Post(u + 0.04, v + 0.06, 2, 0.05, 3, Palette.Poppy);
            }
            else if (palette.Season == Season.Autumn && variant % 2 == 1)
            {
                p.Post(u, v + 0.1, 0, 0.11, 6, Palette.Pumpkin);
            }
        }

        private static void Chair(IsoPainter p, double u, double v, Rgba colour)
        {
            p.Box(u - 0.12, v - 0.08, 0, 0.24, 0.16, 10, Palette.WoodDark);
            p.Box(u - 0.12, v - 0.08, 10, 0.24, 0.16, 2, colour);
        }

        private static void HighStool(IsoPainter p, double u, double v)
        {
            p.Post(u, v, 0, 0.04, 16, Palette.Metal);
            p.FlatDisc(u, v, 16, 0.1, Palette.NeonPink);
        }

        private static void Stool(IsoPainter p, double u, double v)
        {
            p.Post(u, v, 0, 0.08, 10, Palette.WoodDark);
        }

        private static void SnowOn(IsoPainter p, Palette palette, bool outdoors, double u, double v, double z, double du, double dv, double cover = 0.8)
        {
            if (outdoors && palette.IsWinter)
            {
                p.Top(u, v, z + 1, du, dv, (s, t) => Noise.Value((int)(s * 8), (int)(t * 8), 77) < cover ? Palette.Snow : Rgba.Clear);
            }
        }

        private static void Maypole(IsoPainter p, Palette palette)
        {
            // A ring of stones around a pole with bunting: the place the village gathers.
            for (var i = 0; i < 10; i++)
            {
                var a = i / 10.0 * Math.PI * 2;
                p.Post(0.5 + (Math.Cos(a) * 0.42), 0.5 + (Math.Sin(a) * 0.42), 0, 0.05, 3, Palette.Stone);
            }

            p.Post(0.5, 0.5, 0, 0.04, 62, Palette.WoodLight);
            p.Post(0.5, 0.5, 62, 0.07, 4, palette.Accent);
            var (tx, ty) = p.Project(0.5, 0.5, 60);
            for (var side = -1; side <= 1; side += 2)
            {
                for (var k = 1; k <= 5; k++)
                {
                    var x = tx + (side * k * 5);
                    var y = ty + (k * k * 0.8) + 4;
                    p.Canvas.FillRect((int)x, (int)y, 1, 1, Palette.WoodDark);
                    for (var r = 0; r < 4; r++)
                    {
                        p.Canvas.FillRect((int)x - 1 + (r / 2), (int)y + 1 + r, 3 - r, 1, Palette.Bunting[(k + side + 4) % Palette.Bunting.Length]);
                    }
                }
            }
        }
    }
}
