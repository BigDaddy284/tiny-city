using System;
using System.IO;
using System.Linq;
using TinyTown.Art;
using TinyTown.Simulation.Content;
using TinyTown.Simulation.Time;

namespace TinyTown.Tools.ArtPreview
{
    /// <summary>Writes every map (the village in every season, interiors in summer), by day and by night, as PNG files.</summary>
    internal static class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--palette")
            {
                WritePalette(args.Length > 1 ? args[1] : "palette.png");
                return;
            }

            var output = args.Length > 0 ? args[0] : "art-preview";
            Directory.CreateDirectory(output);
            var world = StarterTown.CreateWorld();
            foreach (Season season in Enum.GetValues(typeof(Season)))
            {
                var palette = Palette.For(season);
                foreach (var map in world.Maps)
                {
                    if (!map.IsExterior && season != Season.Summer)
                    {
                        continue;
                    }

                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    var art = MapArt.Build(world, map, palette);
                    var built = watch.ElapsedMilliseconds;
                    var name = $"{map.Id}-{season}".ToLowerInvariant();
                    Png.Write(Path.Combine(output, $"{name}.png"), Compose(art, Lighting.At(12 * 60, season, map.IsExterior)));
                    Png.Write(Path.Combine(output, $"{name}-night.png"), Compose(art, Lighting.At(23 * 60, season, map.IsExterior)));
                    Console.WriteLine($"{name}: {art.Pieces.Count} pieces, drawn in {built} ms");
                }
            }
        }

        /// <summary>
        /// Writes a swatch sheet of the locked palette (Documentation/Images/palette.png) and prints its table for
        /// Documentation/ArtStyle.md.
        /// </summary>
        private static void WritePalette(string path)
        {
            var entries = Palette.Entries().ToList();
            const int columns = 8;
            const int cell = 40;
            var rows = (entries.Count + columns - 1) / columns;
            var sheet = new PixelCanvas(columns * cell, rows * cell, 0, 0);
            for (var i = 0; i < entries.Count; i++)
            {
                var x = (i % columns) * cell;
                var y = (i / columns) * cell;
                sheet.FillRect(x, y, cell, cell, Rgba.Hex("#ffffff"));
                sheet.FillRect(x + 2, y + 2, cell - 4, cell - 4, entries[i].colour.WithAlpha(255));
            }

            Png.Write(path, sheet);
            Console.WriteLine("| Name | Colour |");
            Console.WriteLine("| --- | --- |");
            foreach (var (name, colour) in entries)
            {
                var hex = colour.A == 255 ? colour.ToString().Substring(0, 7) : colour.ToString();
                Console.WriteLine($"| {name} | `{hex}` |");
            }
        }

        /// <summary>Lays the pieces out the way the game does: ground first, then by sorting order.</summary>
        private static PixelCanvas Compose(MapArt art, Daylight light)
        {
            const int top = 140;
            var ground = art.Ground;
            var canvas = new PixelCanvas(ground.Width + 40, ground.Height + top, ground.AnchorX + 20, ground.AnchorY + top);
            Blit(canvas, ground, 0, 0, light.Tint, 1);
            foreach (var piece in art.Pieces.OrderBy(p => ((p.SortX + p.SortY) * 10) + (int)p.Layer))
            {
                var glow = piece.Layer == ArtLayer.Glow;
                if (glow && light.WindowGlow <= 0)
                {
                    continue;
                }

                Blit(canvas, piece.Canvas, piece.TileX, piece.TileY, glow ? Lighting.Day : light.Tint, glow ? light.WindowGlow : 1);
            }

            return canvas;
        }

        private static void Blit(PixelCanvas target, PixelCanvas source, int tileX, int tileY, Rgba tint, double alpha)
        {
            var ox = (int)Math.Round(target.AnchorX + ((tileX - tileY) * IsoPainter.HalfTileWidth) - source.AnchorX);
            var oy = (int)Math.Round(target.AnchorY + ((tileX + tileY) * IsoPainter.HalfTileHeight) - source.AnchorY);
            for (var y = 0; y < source.Height; y++)
            {
                for (var x = 0; x < source.Width; x++)
                {
                    var c = source.Get(x, y);
                    if (c.IsClear)
                    {
                        continue;
                    }

                    var lit = new Rgba((byte)(c.R * tint.R / 255), (byte)(c.G * tint.G / 255), (byte)(c.B * tint.B / 255), (byte)(c.A * alpha));
                    target.Paint(ox + x, oy + y, lit);
                }
            }
        }
    }
}
