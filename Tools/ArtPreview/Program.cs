using System;
using System.IO;
using System.Linq;
using TinyCity.Art;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;

namespace TinyCity.Tools.ArtPreview
{
    /// <summary>Writes every map (the village in every season, interiors in summer), by day and by night, as PNG files; <c>--sounds</c> writes every sound as WAV.</summary>
    internal static class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--scene")
            {
                WriteScenes(args.Length > 1 ? args[1] : "scenes", args.Length > 2 ? int.Parse(args[2]) : 8 * 60);
                return;
            }

            if (args.Length > 0 && args[0] == "--residents")
            {
                WriteResidents(args.Length > 1 ? args[1] : "residents.png");
                return;
            }

            if (args.Length > 0 && args[0] == "--sounds")
            {
                var folder = args.Length > 1 ? args[1] : "sounds";
                Directory.CreateDirectory(folder);
                foreach (var key in TinyCity.Sound.SoundLibrary.Keys)
                {
                    var sound = TinyCity.Sound.SoundLibrary.Generate(key);
                    File.WriteAllBytes(Path.Combine(folder, key + ".wav"), TinyCity.Sound.Wav.Write(sound.Samples, sound.SampleRate));
                    Console.WriteLine($"{key}: {sound.Seconds:0.0} s");
                }

                return;
            }

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

        /// <summary>
        /// Writes a sheet of every starter resident in every pose and facing, with the mood icons along the bottom,
        /// at twice the size on a floor-coloured background.
        /// </summary>
        private static void WriteResidents(string path)
        {
            var world = StarterTown.CreateWorld();
            var residents = StarterTown.CreateResidents(world);
            var poses = new System.Collections.Generic.List<ResidentPose>();
            foreach (var facing in new[] { Facing.DownRight, Facing.DownLeft, Facing.UpLeft, Facing.UpRight })
            {
                for (var f = 0; f < 4; f++)
                {
                    poses.Add(new ResidentPose(ResidentAction.Walk, Stance.Standing, facing, f));
                }
            }

            foreach (ResidentAction action in Enum.GetValues(typeof(ResidentAction)))
            {
                for (var f = 0; f < ResidentPose.FramesOf(action); f++)
                {
                    poses.Add(new ResidentPose(action, Stance.Standing, Facing.DownRight, f));
                }
            }

            foreach (var action in new[] { ResidentAction.Stand, ResidentAction.Eat, ResidentAction.Read, ResidentAction.Work, ResidentAction.Talk })
            {
                poses.Add(new ResidentPose(action, Stance.Sitting, Facing.DownRight, 1));
                poses.Add(new ResidentPose(action, Stance.Sitting, Facing.UpRight, 1));
            }

            // The starter residents, then children born in the city: a child, a teenager and a baby.
            var looks = residents.Select(r => ResidentLook.For(r.Id, r.AgeYears)).ToList();
            looks.Add(ResidentLook.For("res-0021", 7));
            looks.Add(ResidentLook.For("res-0022", 15));
            looks.Add(ResidentLook.For("res-0023", 1));

            const int scale = 2;
            var cellW = ResidentArt.Width;
            var cellH = ResidentArt.Height;
            var sheet = new PixelCanvas(poses.Count * cellW * scale, (looks.Count * cellH + 16) * scale, 0, 0);
            sheet.FillRect(0, 0, sheet.Width, sheet.Height, Palette.WoodLight);
            for (var r = 0; r < looks.Count; r++)
            {
                for (var i = 0; i < poses.Count; i++)
                {
                    Stamp(sheet, ResidentArt.Render(looks[r], poses[i]), i * cellW * scale, r * cellH * scale, scale);
                }
            }

            var x = 0;
            foreach (var mood in MoodArt.Shown)
            {
                Stamp(sheet, MoodArt.Icon(mood), x, looks.Count * cellH * scale + scale, scale);
                x += (MoodArt.Width + 3) * scale;
            }

            Png.Write(path, sheet);
            Console.WriteLine($"{looks.Count} looks x {poses.Count} poses");
        }

        private static void Stamp(PixelCanvas target, PixelCanvas source, int ox, int oy, int scale)
        {
            for (var y = 0; y < source.Height * scale; y++)
            {
                for (var x = 0; x < source.Width * scale; x++)
                {
                    target.Paint(ox + x, oy + y, source.Get(x / scale, y / scale));
                }
            }
        }

        /// <summary>
        /// Runs a new town for <paramref name="minutes"/> and draws every map with the residents where they are,
        /// posed and placed as the game does, with their mood icons.
        /// </summary>
        private static void WriteScenes(string output, int minutes)
        {
            Directory.CreateDirectory(output);
            var sim = StarterTown.CreateNewGame();
            sim.Advance(minutes);
            var world = sim.World;
            var season = Seasons.Of(sim.Time);
            var palette = Palette.For(season);
            var seating = new Seating();
            seating.Update(world, sim.Residents);
            foreach (var map in world.Maps)
            {
                var here = sim.Residents.Where(r => r.IsAlive && r.Location.MapId == map.Id).ToList();
                if (here.Count == 0)
                {
                    continue;
                }

                var art = MapArt.Build(world, map, palette);
                var extra = new System.Collections.Generic.List<(int sortX, int sortY, int layer, PixelCanvas canvas, double px, double py)>();
                foreach (var r in here)
                {
                    var (action, placement) = ResidentPose.Choose(world, r, false, seating.SeatOf(r.Id));
                    var look = ResidentLook.For(r.Id, r.AgeYears);
                    var pose = new ResidentPose(action, placement.Stance, placement.Facing, 1);
                    var dx = ((placement.U - placement.V) * IsoPainter.HalfTileWidth);
                    var dy = ((placement.U + placement.V - 1) * IsoPainter.HalfTileHeight) - placement.Z;
                    var pos = r.Location.Position;
                    extra.Add((pos.X, pos.Y, placement.Behind ? 0 : 3, ResidentArt.Render(look, pose), dx, dy));
                    var icon = MoodArt.Icon(Moods.Of(sim, r));
                    if (icon != null)
                    {
                        extra.Add((pos.X, pos.Y, 9, icon, dx, dy - ResidentArt.HeadTop(look, pose) - 2));
                    }

                    Console.WriteLine($"{map.Id}: {r.FirstName} {r.State} {r.ActivityId} -> {action} {placement.Stance} {Moods.Of(sim, r)}");
                }

                var light = Lighting.At(sim.Time.MinuteOfDay, season, map.IsExterior);
                var canvas = Compose(art, light, extra);
                Png.Write(Path.Combine(output, $"{map.Id}.png"), canvas);
            }

            Console.WriteLine($"Day {sim.Time.Day} {sim.Time.ClockText}");
        }

        /// <summary>Lays the pieces out the way the game does: ground first, then by sorting order.</summary>
        private static PixelCanvas Compose(MapArt art, Daylight light) => Compose(art, light, null);

        private static PixelCanvas Compose(MapArt art, Daylight light, System.Collections.Generic.List<(int sortX, int sortY, int layer, PixelCanvas canvas, double px, double py)> extra)
        {
            const int top = 140;
            var ground = art.Ground;
            var canvas = new PixelCanvas(ground.Width + 40, ground.Height + top, ground.AnchorX + 20, ground.AnchorY + top);
            Blit(canvas, ground, 0, 0, light.Tint, 1);
            var all = art.Pieces.Select(p => (order: ((p.SortX + p.SortY) * 10) + (int)p.Layer, draw: (Action)(() =>
            {
                var glow = p.Layer == ArtLayer.Glow;
                if (!glow || light.WindowGlow > 0)
                {
                    Blit(canvas, p.Canvas, p.TileX, p.TileY, glow ? Lighting.Day : light.Tint, glow ? light.WindowGlow : 1);
                }
            }))).ToList();
            foreach (var e in extra ?? new System.Collections.Generic.List<(int sortX, int sortY, int layer, PixelCanvas canvas, double px, double py)>())
            {
                var tint = e.layer > 3 ? Lighting.Day : light.Tint;
                all.Add((((e.sortX + e.sortY) * 10) + e.layer + (e.layer > 3 ? 100000 : 0), () => Blit(canvas, e.canvas, e.sortX, e.sortY, tint, 1, e.px, e.py)));
            }

            foreach (var item in all.OrderBy(a => a.order))
            {
                item.draw();
            }

            return canvas;
        }

        private static void Blit(PixelCanvas target, PixelCanvas source, int tileX, int tileY, Rgba tint, double alpha, double px = 0, double py = 0)
        {
            var ox = (int)Math.Round(target.AnchorX + ((tileX - tileY) * IsoPainter.HalfTileWidth) - source.AnchorX + px);
            var oy = (int)Math.Round(target.AnchorY + ((tileX + tileY) * IsoPainter.HalfTileHeight) - source.AnchorY + py);
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
