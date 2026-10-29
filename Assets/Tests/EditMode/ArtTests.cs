using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyCity.Art;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Time;
using TinyCity.Simulation.World;

namespace TinyCity.Tests.EditMode
{
    /// <summary>Phase 6, build 1: the generated art for places, seasons and the light.</summary>
    public class ArtTests
    {
        private static readonly TownWorld World = StarterTown.CreateWorld();

        [Test]
        public void Seasons_ShareTheYearEqually_SpringFirst()
        {
            Assert.That(Seasons.DaysPerSeason, Is.EqualTo(7));
            Assert.That(Seasons.Of(1), Is.EqualTo(Season.Spring));
            Assert.That(Seasons.Of(7), Is.EqualTo(Season.Spring));
            Assert.That(Seasons.Of(8), Is.EqualTo(Season.Summer));
            Assert.That(Seasons.Of(15), Is.EqualTo(Season.Autumn));
            Assert.That(Seasons.Of(28), Is.EqualTo(Season.Winter));
            Assert.That(Seasons.Of(29), Is.EqualTo(Season.Spring), "a new year starts with spring");
            Assert.That(Seasons.DayOfSeason(9), Is.EqualTo(2));
            Assert.That(Seasons.Of(GameTime.FromDayAndTime(22, 23, 59)), Is.EqualTo(Season.Winter));
        }

        [Test]
        public void Rgba_ParsesHex_AndBlends()
        {
            var red = Rgba.Hex("#ff0000");
            Assert.That(red.R == 255 && red.G == 0 && red.B == 0 && red.A == 255, Is.True);
            Assert.That(Rgba.Hex("#00000080").A, Is.EqualTo(128));
            var half = new Rgba(0, 0, 255, 128).Over(red);
            Assert.That(half.R > 100 && half.R < 150 && half.B > 100 && half.B < 150 && half.A == 255, Is.True, half.ToString());
            Assert.That(Rgba.Clear.Over(red), Is.EqualTo(red));
            Assert.That(red.Shade(0.5).R, Is.EqualTo(128));
        }

        [Test]
        public void Canvas_GivesTextureBytes_BottomRowFirst()
        {
            var canvas = new PixelCanvas(3, 2, 1, 1);
            canvas.Set(0, 0, Rgba.Hex("#112233"));
            canvas.Set(2, 1, Rgba.Hex("#445566"));
            var bytes = canvas.ToBytesBottomUp();
            Assert.That(bytes.Length, Is.EqualTo(3 * 2 * 4));
            Assert.That(bytes[0], Is.EqualTo(0), "the bottom row comes first, and its first pixel is empty");
            Assert.That(bytes[8] == 0x44 && bytes[9] == 0x55 && bytes[10] == 0x66 && bytes[11] == 255, Is.True);
            Assert.That(bytes[12] == 0x11 && bytes[13] == 0x22 && bytes[14] == 0x33, Is.True, "the top row comes last");
        }

        [Test]
        public void PngReader_ReadsRgbaPictures_WithEveryFilter()
        {
            // Saved by an image editor (Pillow, optimised), so its rows use a mix of PNG filters.
            var canvas = PngReader.Read(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAUAAAADCAYAAABbNsX4AAAANklEQVR42gXBsRGAMBADwfOMo48VQ3qxa6IQyqMrsUuhlBfK5Or3nG4VBZSJaNgqq8AqkxsNPwLbDPjFzdyNAAAAAElFTkSuQmCC"), (w, h) => (w / 2.0, h - 1.0));
            Assert.That(canvas.Width == 5 && canvas.Height == 3, Is.True);
            Assert.That(canvas.AnchorX == 2.5 && canvas.AnchorY == 2.0, Is.True, "the anchor comes from the caller");
            Assert.That(canvas.Get(0, 0), Is.EqualTo(new Rgba(255, 0, 0)));
            Assert.That(canvas.Get(1, 0), Is.EqualTo(new Rgba(0, 255, 0, 128)));
            Assert.That(canvas.Get(2, 0).IsClear, Is.True);
            Assert.That(canvas.Get(4, 1), Is.EqualTo(new Rgba(240, 140, 90)));
            Assert.That(canvas.Get(3, 2), Is.EqualTo(new Rgba(90, 100, 110)));
            Assert.That(canvas.Get(4, 2), Is.EqualTo(new Rgba(24, 180, 130)));
        }

        [Test]
        public void PngReader_ReadsPalettePictures_AndRejectsBadFiles()
        {
            var canvas = PngReader.Read(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAUAAAADCAMAAABs6DXKAAACplBMVEUAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAzAABmAACZAADMAAD/AAAAMwAzMwBmMwCZMwDMMwD/MwAAZgAzZgBmZgCZZgDMZgD/ZgAAmQAzmQBmmQCZmQDMmQD/mQAAzAAzzABmzACZzADMzAD/zAAA/wAz/wBm/wCZ/wDM/wD//wAAADMzADNmADOZADPMADP/ADMAMzMzMzNmMzOZMzPMMzP/MzMAZjMzZjNmZjOZZjPMZjP/ZjMAmTMzmTNmmTOZmTPMmTP/mTMAzDMzzDNmzDOZzDPMzDP/zDMA/zMz/zNm/zOZ/zPM/zP//zMAAGYzAGZmAGaZAGbMAGb/AGYAM2YzM2ZmM2aZM2bMM2b/M2YAZmYzZmZmZmaZZmbMZmb/ZmYAmWYzmWZmmWaZmWbMmWb/mWYAzGYzzGZmzGaZzGbMzGb/zGYA/2Yz/2Zm/2aZ/2bM/2b//2YAAJkzAJlmAJmZAJnMAJn/AJkAM5kzM5lmM5mZM5nMM5n/M5kAZpkzZplmZpmZZpnMZpn/ZpkAmZkzmZlmmZmZmZnMmZn/mZkAzJkzzJlmzJmZzJnMzJn/zJkA/5kz/5lm/5mZ/5nM/5n//5kAAMwzAMxmAMyZAMzMAMz/AMwAM8wzM8xmM8yZM8zMM8z/M8wAZswzZsxmZsyZZszMZsz/ZswAmcwzmcxmmcyZmczMmcz/mcwAzMwzzMxmzMyZzMzMzMz/zMwA/8wz/8xm/8yZ/8zM/8z//8wAAP8zAP9mAP+ZAP/MAP//AP8AM/8zM/9mM/+ZM//MM///M/8AZv8zZv9mZv+ZZv/MZv//Zv8Amf8zmf9mmf+Zmf/Mmf//mf8AzP8zzP9mzP+ZzP/MzP//zP8A//8z//9m//+Z///M//////91g91EAAAAGElEQVR4nGPg19inZ8dgCgSZDKYJpgl9ACOQBFcjXFaCAAAAAElFTkSuQmCC"), (w, h) => (0, 0));
            Assert.That(canvas.Get(0, 0), Is.EqualTo(new Rgba(255, 0, 0)));
            Assert.That(canvas.Get(4, 0), Is.EqualTo(new Rgba(204, 102, 51)));
            Assert.That(canvas.Get(4, 2), Is.EqualTo(new Rgba(0, 204, 153)));

            Assert.Throws<System.IO.InvalidDataException>(() => PngReader.Read(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, (w, h) => (0, 0)));
            Assert.Throws<System.IO.InvalidDataException>(() => PngReader.Read(null, (w, h) => (0, 0)));
            var cut = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAUAAAADCAYAAABbNsX4AAAANklEQVR42gXBsRGAMBADwfOMo48VQ3qxa6IQyqMrsUuhlBfK5Or3nG4VBZSJaNgqq8AqkxsNPwLbDPjFzdyNAAAAAElFTkSuQmCC").Take(40).ToArray();
            Assert.Throws<System.IO.InvalidDataException>(() => PngReader.Read(cut, (w, h) => (0, 0)));
        }

        [Test]
        public void Painter_ProjectsTheAnchorTileCentre_OntoTheAnchor()
        {
            var painter = new IsoPainter(new PixelCanvas(64, 64, 32, 40));
            Assert.That(painter.Project(0.5, 0.5, 0), Is.EqualTo((32.0, 40.0)));
            Assert.That(painter.Project(1.5, 0.5, 0), Is.EqualTo((64.0, 56.0)), "one tile along x is half a tile right and down");
            Assert.That(painter.Project(0.5, 0.5, 10), Is.EqualTo((32.0, 30.0)), "height goes straight up");
        }

        [Test]
        public void Palette_ChangesWithTheSeasons()
        {
            var grass = Enum.GetValues(typeof(Season)).Cast<Season>().Select(s => Palette.For(s).Grass).ToList();
            Assert.That(grass.Distinct().Count(), Is.EqualTo(4));
            Assert.That(Palette.For(Season.Winter).IsWinter, Is.True);
            var snow = Palette.For(Season.Winter).Grass;
            Assert.That(snow.R > 200 && snow.G > 200 && snow.B > 200, Is.True, "winter grass is under snow");
        }

        [Test]
        public void Buildings_AreFoundForEveryHouseBlock_WithTheirDoorAndPlace()
        {
            World.TryGetMap(StarterTown.VillageMapId, out var village);
            var buildings = BuildingArt.Find(World, village);
            Assert.That(buildings.Count, Is.EqualTo(27));
            Assert.That(buildings.Count(b => b.Kind == PlaceKind.Cottage), Is.EqualTo(16));
            Assert.That(buildings.Count(b => b.Kind == PlaceKind.Apartments), Is.EqualTo(2));
            foreach (var kind in new[] { PlaceKind.Store, PlaceKind.Cafe, PlaceKind.Bakery, PlaceKind.Clinic, PlaceKind.Bar, PlaceKind.CueClub, PlaceKind.Bowling, PlaceKind.Arcade, PlaceKind.Gym })
            {
                Assert.That(buildings.Count(b => b.Kind == kind), Is.EqualTo(1), kind.ToString());
            }

            foreach (var b in buildings)
            {
                if (b.Kind == PlaceKind.Apartments)
                {
                    Assert.That(b.Width == 6 && b.Depth == 6, Is.True, $"{b.InteriorMapId} is {b.Width}x{b.Depth}");
                    Assert.That(b.Doors.Count, Is.EqualTo(8), "a door to each flat");
                    Assert.That(b.Doors.Select(d => World.Portals.First(p => p.From.MapId == village.Id && p.From.Position.Equals(d)).To.MapId).Distinct().Count(), Is.EqualTo(8));
                }
                else
                {
                    Assert.That(b.Width >= 5 && b.Width <= 8 && b.Depth >= 3 && b.Depth <= 4, Is.True, $"{b.InteriorMapId} is {b.Width}x{b.Depth}");
                    Assert.That(b.Doors.Count, Is.EqualTo(1));
                }

                Assert.That(b.Contains(b.Door.X, b.Door.Y), Is.True, $"{b.InteriorMapId}'s door is part of the building");
                Assert.That(b.Door.Y, Is.EqualTo(b.Y + b.Depth - 1), "doors are on the front row");
                Assert.That(village.GetTile(b.Door), Is.EqualTo(TileType.Door));
                Assert.That(World.TryGetMap(b.InteriorMapId, out _), Is.True);
            }

            Assert.That(buildings.Select(b => b.InteriorMapId).Distinct().Count(), Is.EqualTo(27));
        }

        [Test]
        public void BuildingStrips_CoverThePicture_AndSortByTheirFrontTile()
        {
            World.TryGetMap(StarterTown.VillageMapId, out var village);
            var plan = BuildingArt.Find(World, village).First(b => b.Kind == PlaceKind.Cafe);
            var (body, _) = BuildingArt.Render(plan, Palette.For(Season.Summer));
            var strips = BuildingArt.Strips(body, plan);
            Assert.That(strips.Count, Is.GreaterThan(plan.Width + plan.Depth - 2));

            var painted = Count(body);
            Assert.That(strips.Sum(s => Count(s.strip)), Is.EqualTo(painted), "no pixel is lost or doubled");
            foreach (var (strip, tileX, tileY) in strips)
            {
                Assert.That(strip.Width, Is.LessThanOrEqualTo(IsoPainter.HalfTileWidth));
                Assert.That(plan.Contains(tileX, tileY), Is.True, $"strip sorts by ({tileX}, {tileY}), outside the footprint");
            }

            // The strip over the door sorts by the front row, so someone at the door stands in front of the wall.
            Assert.That(strips.Any(s => s.tileX == plan.Door.X && s.tileY == plan.Door.Y), Is.True);

            // Every footprint tile on a strip's two diagonals is no nearer the viewer than its sorting tile.
            for (var k = -plan.Depth; k <= plan.Width; k++)
            {
                var (fi, fj) = BuildingArt.FrontTile(plan, k);
                for (var i = 0; i < plan.Width; i++)
                {
                    for (var j = 0; j < plan.Depth; j++)
                    {
                        if (i - j == k || i - j == k + 1)
                        {
                            Assert.That(i + j, Is.LessThanOrEqualTo(fi + fj), $"strip {k}: tile ({i}, {j}) is in front of ({fi}, {fj})");
                        }
                    }
                }
            }
        }

        [Test]
        public void Buildings_LightTheirWindows_OnlyInTheGlowPicture()
        {
            World.TryGetMap(StarterTown.VillageMapId, out var village);
            foreach (var plan in BuildingArt.Find(World, village))
            {
                var (body, glow) = BuildingArt.Render(plan, Palette.For(Season.Autumn));
                Assert.That(Count(body), Is.GreaterThan(5000), plan.InteriorMapId);
                Assert.That(Count(glow), Is.GreaterThan(50), $"{plan.InteriorMapId} has lit windows");
                Assert.That(Count(glow), Is.LessThan(Count(body) / 4));
            }
        }

        [Test]
        public void EveryObjectType_HasAPicture_IndoorsAndOut()
        {
            foreach (ObjectType type in Enum.GetValues(typeof(ObjectType)))
            {
                foreach (var outdoors in new[] { false, true })
                {
                    var canvas = FurnitureArt.Render(type, 3, Palette.For(Season.Winter), outdoors);
                    Assert.That(Count(canvas), Is.GreaterThan(60), $"{type} outdoors={outdoors}");
                }
            }
        }

        [Test]
        public void Trees_ChangeWithTheSeasons_ButPinesStayGreen()
        {
            var broadleaf = Enumerable.Range(0, 50).First(v => !NatureArt.IsPine(v));
            var pine = Enumerable.Range(0, 50).First(NatureArt.IsPine);
            var summer = NatureArt.Tree(Palette.For(Season.Summer), broadleaf);
            var winter = NatureArt.Tree(Palette.For(Season.Winter), broadleaf);
            Assert.That(Count(winter), Is.LessThan(Count(summer) * 0.7), "bare branches in winter");
            Assert.That(Same(NatureArt.Tree(Palette.For(Season.Summer), broadleaf), summer), Is.True, "trees are drawn the same way every time");
            var pineSummer = NatureArt.Tree(Palette.For(Season.Summer), pine);
            var pineWinter = NatureArt.Tree(Palette.For(Season.Winter), pine);
            Assert.That(Math.Abs(Count(pineSummer) - Count(pineWinter)), Is.LessThan(Count(pineSummer) / 10), "pines keep their needles");
            Assert.That(Same(pineSummer, pineWinter), Is.False, "but carry snow");
        }

        [Test]
        public void EveryMap_GetsGround_AndAPictureForEachTreeWallAndObject()
        {
            foreach (var map in World.Maps)
            {
                var art = MapArt.Build(World, map, Palette.For(Season.Spring));
                Assert.That(art.Ground.Width, Is.EqualTo((map.Width + map.Height) * IsoPainter.HalfTileWidth), map.Id);
                Assert.That(Count(art.Ground), Is.GreaterThan(map.Width * map.Height * 200), $"{map.Id} ground is mostly painted");

                var objects = World.Objects.Where(o => o.Location.MapId == map.Id).ToList();
                foreach (var obj in objects)
                {
                    Assert.That(art.Pieces.Count(p => p.Name == obj.Name && p.TileX == obj.Location.Position.X && p.TileY == obj.Location.Position.Y), Is.GreaterThanOrEqualTo(1), $"{obj.Id} has a picture");
                }

                var trees = Tiles(map).Count(t => map.GetTile(t) == TileType.Tree);
                Assert.That(art.Pieces.Count(p => p.Name.StartsWith("Tree ", StringComparison.Ordinal)), Is.EqualTo(trees), map.Id);
                if (map.IsExterior)
                {
                    Assert.That(art.Pieces.Count(p => p.Layer == ArtLayer.Glow), Is.GreaterThan(14));
                }
                else
                {
                    var walls = Tiles(map).Count(t => map.GetTile(t) == TileType.Wall);
                    Assert.That(art.Pieces.Count(p => p.Name.StartsWith("Wall ", StringComparison.Ordinal)), Is.GreaterThanOrEqualTo(walls), map.Id);
                    Assert.That(art.Pieces.All(p => p.Layer == ArtLayer.Solid), Is.True, "rooms have no window glow");
                }

                foreach (var piece in art.Pieces)
                {
                    Assert.That(map.InBounds(new GridPos(piece.SortX, piece.SortY)), Is.True, $"{map.Id}: {piece.Name}");
                }
            }
        }

        [Test]
        public void Art_IsTheSameEveryTime_AndDiffersBetweenSummerAndWinter()
        {
            World.TryGetMap(StarterTown.VillageMapId, out var village);
            var a = GroundArt.Render(village, PlaceKind.Village, Palette.For(Season.Summer));
            var b = GroundArt.Render(village, PlaceKind.Village, Palette.For(Season.Summer));
            var winter = GroundArt.Render(village, PlaceKind.Village, Palette.For(Season.Winter));
            Assert.That(Same(a, b), Is.True);
            Assert.That(Same(a, winter), Is.False);
        }

        [Test]
        public void Replacements_AreUsed_SeasonalOnesFirst()
        {
            World.TryGetMap(StarterTown.VillageMapId, out var village);
            var asked = new List<string>();
            var custom = new PixelCanvas(10, 20, 5, 4);
            var winterCustom = new PixelCanvas(12, 20, 6, 4);
            var art = MapArt.Build(World, village, Palette.For(Season.Winter), request =>
            {
                asked.Add(request.Key);
                return request.Key == "tree-winter" ? winterCustom : request.Key == "object-bench" ? custom : null;
            });

            Assert.That(asked, Does.Contain("ground-map-village-winter"));
            Assert.That(asked, Does.Contain("building-map-cafe-winter"));
            Assert.That(art.Pieces.Where(p => p.Name.StartsWith("Tree ", StringComparison.Ordinal) && p.Canvas == winterCustom).Count(), Is.GreaterThan(10));
            Assert.That(art.Pieces.Count(p => p.Canvas == custom), Is.EqualTo(World.ObjectsOfType(ObjectType.Bench).Count(o => o.Location.MapId == village.Id)));
        }

        [Test]
        public void ReplacementAnchor_CentresOnTheFootprint_WithTheBottomOnItsNearestCorner()
        {
            Assert.That(MapArt.AnchorFor(64, 100, 1, 1), Is.EqualTo((32.0, 84.0)));
            var plan = BuildingArt.NewCanvas(new BuildingPlan(0, 0, 5, 4, new GridPos(2, 3), PlaceKind.Cottage, 0, null));
            var (ax, _) = MapArt.AnchorFor(plan.Width, plan.Height, 5, 4);
            Assert.That(ax, Is.EqualTo(plan.AnchorX), "a picture as wide as a generated one lines up the same way");
        }

        [Test]
        public void Light_IsFullByDay_DarkAtNight_AndLampLitIndoors()
        {
            var noon = Lighting.At(12 * 60, Season.Summer, outdoors: true);
            Assert.That(noon.Tint, Is.EqualTo(Lighting.Day));
            Assert.That(noon.WindowGlow, Is.EqualTo(0));

            var midnight = Lighting.At(0, Season.Summer, outdoors: true);
            Assert.That(midnight.Tint, Is.EqualTo(Lighting.Night));
            Assert.That(midnight.WindowGlow, Is.EqualTo(1));

            var indoorsAtNight = Lighting.At(23 * 60, Season.Winter, outdoors: false);
            Assert.That(indoorsAtNight.Tint, Is.EqualTo(Lighting.Lamplight));
            Assert.That(indoorsAtNight.WindowGlow, Is.EqualTo(0));
            Assert.That(Lighting.At(12 * 60, Season.Winter, outdoors: false).Tint, Is.EqualTo(Lighting.Day));

            // Summer evenings are long; winter ones are not.
            var summerEvening = Lighting.At((20 * 60) + 30, Season.Summer, outdoors: true);
            var winterEvening = Lighting.At((20 * 60) + 30, Season.Winter, outdoors: true);
            Assert.That(summerEvening.Tint, Is.EqualTo(Lighting.Day));
            Assert.That(winterEvening.Tint, Is.EqualTo(Lighting.Night));

            // Dusk is gradual.
            var (_, sunset) = Lighting.SunTimes(Season.Autumn);
            var before = Lighting.At(sunset - 20, Season.Autumn, outdoors: true);
            var after = Lighting.At(sunset + 20, Season.Autumn, outdoors: true);
            Assert.That(before.Tint.B > after.Tint.B || before.Tint.R > after.Tint.R, Is.True);
            Assert.That(after.WindowGlow, Is.GreaterThan(before.WindowGlow));
        }

        private static IEnumerable<GridPos> Tiles(TileMap map)
        {
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    yield return new GridPos(x, y);
                }
            }
        }

        private static int Count(PixelCanvas canvas) => canvas.Pixels.Count(p => !p.IsClear);

        private static bool Same(PixelCanvas a, PixelCanvas b)
        {
            return a.Width == b.Width && a.Height == b.Height && a.Pixels.SequenceEqual(b.Pixels);
        }
    }
}
