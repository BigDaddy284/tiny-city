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
                case ObjectType.Sink:
                    p.Box(0.15, 0.25, 0, 0.7, 0.62, 19, Palette.WoodLight);
                    p.Top(0.15, 0.25, 19, 0.7, 0.62, (s, t) => s > 0.2 && s < 0.8 && t > 0.2 && t < 0.8 ? Palette.GlassLight : Palette.Porcelain);
                    p.Post(0.5, 0.3, 19, 0.04, 9, Palette.Metal);
                    break;
                case ObjectType.Toilet:
                    p.Box(0.28, 0.18, 0, 0.44, 0.16, 26, Palette.Porcelain);
                    p.Post(0.5, 0.55, 0, 0.17, 12, Palette.Porcelain);
                    p.FlatDisc(0.5, 0.55, 13, 0.15, Palette.PorcelainShade);
                    break;
                case ObjectType.Shower:
                    p.Box(0.08, 0.08, 0, 0.84, 0.84, 3, Palette.Porcelain);
                    p.Post(0.15, 0.15, 3, 0.03, 44, Palette.Metal);
                    p.Box(0.15, 0.15, 44, 0.3, 0.06, 3, Palette.Metal);
                    p.LeftWall(0.1, 0.9, 3, 0.82, 40, (s, t) => Palette.ShowerGlass.WithAlpha((byte)(s < 0.04 || s > 0.96 ? 200 : 70)));
                    p.RightWall(0.92, 0.1, 3, 0.8, 40, (s, t) => Palette.ShowerGlass.WithAlpha((byte)(s > 0.96 ? 200 : 60)));
                    break;
                case ObjectType.Armchair:
                    p.Box(0.18, 0.18, 0, 0.64, 0.64, 10, accent.Shade(0.9));
                    p.Box(0.12, 0.16, 0, 0.16, 0.68, 26, accent.Shade(0.85));
                    p.Box(0.18, 0.12, 0, 0.64, 0.1, 17, accent);
                    p.Box(0.18, 0.8, 0, 0.64, 0.1, 17, accent);
                    p.Box(0.28, 0.24, 10, 0.5, 0.54, 3, accent.Mix(Palette.White, 0.25));
                    break;
                case ObjectType.GardenPlot:
                    p.Box(0.05, 0.05, 0, 0.9, 0.06, 5, Palette.Wood);
                    p.Box(0.05, 0.89, 0, 0.9, 0.06, 5, Palette.Wood);
                    for (var i = 0; i < 3; i++)
                    {
                        Plant(p, palette, 0.25 + (i * 0.25), 0.5, variant + i);
                    }

                    break;
                case ObjectType.GardenTap:
                    p.Box(0.42, 0.42, 0, 0.14, 0.14, 22, Palette.WoodDark);
                    p.Box(0.38, 0.56, 16, 0.08, 0.14, 3, Palette.Metal);
                    p.Post(0.7, 0.66, 0, 0.12, 10, Palette.LeafGreen);
                    p.Box(0.8, 0.62, 6, 0.16, 0.05, 2, Palette.LeafGreen);
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
                case ObjectType.ExerciseSpot:
                    p.Top(0.12, 0.2, 0.5, 0.76, 0.6, (s, t) => (int)(s * 6) % 2 == 0 ? Palette.Mat : Palette.MatDark);
                    p.Post(0.28, 0.36, 1, 0.07, 4, Palette.Charcoal);
                    p.Post(0.72, 0.36, 1, 0.07, 4, Palette.Charcoal);
                    p.Box(0.3, 0.33, 3, 0.4, 0.05, 2, Palette.Metal);
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
                case ObjectType.ShopShelf:
                    p.Box(0.1, 0.1, 0, 0.8, 0.4, 46, Palette.Wood);
                    p.LeftWall(0.12, 0.5, 2, 0.76, 42, (s, t) =>
                    {
                        var shelf = (int)(t * 4);
                        if ((t * 4) - shelf < 0.12)
                        {
                            return Palette.WoodDark;
                        }

                        var item = (int)(s * 6);
                        return Noise.Value(item, shelf, variant) < 0.8 ? Palette.Goods[(item + shelf) % Palette.Goods.Length].Shade(0.9) : Palette.WoodDark.Shade(0.8);
                    });
                    break;
                case ObjectType.Counter:
                    p.Box(0.05, 0.3, 0, 0.9, 0.45, 22, Palette.Wood);
                    p.LeftWall(0.05, 0.75, 4, 0.9, 14, (s, t) => (int)(s * 5) % 2 == 0 ? Palette.WoodLight.Shade(0.85) : Palette.Wood.Shade(0.85));
                    p.Top(0.05, 0.3, 22, 0.9, 0.45, (s, t) => Palette.WoodLight);
                    p.Box(0.6, 0.38, 22, 0.24, 0.22, 9, Palette.Slate);
                    p.Box(0.62, 0.38, 31, 0.2, 0.06, 5, Palette.SlateLight);
                    break;
                case ObjectType.CafeTable:
                    Chair(p, 0.5, 0.12, accent);
                    p.Post(0.5, 0.5, 0, 0.05, 18, Palette.WoodDark);
                    p.FlatDisc(0.5, 0.5, 18, 0.3, Palette.WoodDark);
                    p.FlatDisc(0.5, 0.5, 19, 0.28, Palette.WoodLight);
                    p.Post(0.42, 0.45, 19, 0.06, 4, Palette.White);
                    Chair(p, 0.5, 0.82, accent);
                    break;
                case ObjectType.BakeryDisplay:
                    p.Box(0.08, 0.22, 0, 0.84, 0.6, 16, Palette.Wood);
                    p.Top(0.08, 0.22, 16, 0.84, 0.6, (s, t) => Palette.White);
                    for (var i = 0; i < 4; i++)
                    {
                        var colour = i % 2 == 0 ? Palette.Bread : Palette.Icing;
                        p.Post(0.2 + (i * 0.2), 0.5, 16, 0.07, 5, colour);
                    }

                    p.LeftWall(0.08, 0.82, 16, 0.84, 12, (s, t) => Palette.DisplayGlass);
                    p.RightWall(0.92, 0.22, 16, 0.6, 12, (s, t) => Palette.DisplayGlass);
                    p.Top(0.08, 0.22, 28, 0.84, 0.6, (s, t) => Palette.DisplayGlassTop);
                    break;
                case ObjectType.ClinicBed:
                    foreach (var (lu, lv) in new[] { (0.12, 0.2), (0.84, 0.2), (0.12, 0.76), (0.84, 0.76) })
                    {
                        p.Box(lu, lv, 0, 0.05, 0.05, 10, Palette.Metal);
                    }

                    p.Box(0.1, 0.18, 10, 0.82, 0.66, 5, Palette.White);
                    p.Box(0.06, 0.16, 0, 0.06, 0.7, 24, Palette.Metal);
                    p.Box(0.16, 0.24, 15, 0.18, 0.54, 3, Palette.White);
                    p.Box(0.42, 0.2, 15, 0.48, 0.62, 2, Palette.ClinicBlanket);
                    break;
                case ObjectType.Easel:
                    p.Post(0.32, 0.6, 0, 0.03, 34, Palette.WoodDark);
                    p.Post(0.68, 0.6, 0, 0.03, 34, Palette.WoodDark);
                    p.Post(0.5, 0.35, 0, 0.03, 38, Palette.WoodDark);
                    p.LeftWall(0.25, 0.62, 14, 0.5, 22, (s, t) =>
                    {
                        if (s < 0.06 || s > 0.94 || t < 0.06 || t > 0.94)
                        {
                            return Palette.WoodLight;
                        }

                        return t < 0.45 ? palette.Grass : t < 0.55 ? Palette.Yellow : Palette.Sky;
                    });
                    break;
                case ObjectType.Piano:
                    p.Box(0.1, 0.12, 0, 0.8, 0.4, 36, Palette.Ebony);
                    p.Box(0.1, 0.52, 14, 0.8, 0.2, 4, Palette.Ebony);
                    p.Top(0.12, 0.52, 18, 0.76, 0.18, (s, t) => (int)(s * 22) % 2 == 0 ? Palette.White : Palette.Ivory);
                    p.Box(0.2, 0.72, 0, 0.6, 0.12, 9, Palette.WoodDark);
                    break;
                case ObjectType.ChessTable:
                    p.Box(0.2, 0.2, 0, 0.6, 0.6, 16, Palette.Wood);
                    p.Top(0.2, 0.2, 16, 0.6, 0.6, (s, t) => ((int)(s * 6) + (int)(t * 6)) % 2 == 0 ? Palette.White : Palette.Ebony);
                    p.Post(0.35, 0.35, 16, 0.03, 4, Palette.White);
                    p.Post(0.62, 0.6, 16, 0.03, 5, Palette.Ebony);
                    Stool(p, 0.05, 0.5);
                    Stool(p, 0.92, 0.5);
                    SnowOn(p, palette, outdoors, 0.2, 0.2, 16.5, 0.6, 0.6, 0.4);
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
                    if (outdoors)
                    {
                        Maypole(p, palette);
                    }
                    else
                    {
                        // A chalkboard on an easel for the quiz night.
                        p.Post(0.35, 0.5, 0, 0.03, 30, Palette.WoodDark);
                        p.Post(0.65, 0.5, 0, 0.03, 30, Palette.WoodDark);
                        p.LeftWall(0.28, 0.52, 10, 0.44, 22, (s, t) => s < 0.07 || s > 0.93 || t < 0.07 || t > 0.93 ? Palette.Wood : Noise.Value((int)(s * 20), (int)(t * 8), 1) < 0.15 ? Palette.White : Palette.Chalkboard);
                    }

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
