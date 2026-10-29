using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TinyCity.Art;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Time;
using TinyCity.Simulation.World;

namespace TinyCity.Tests.EditMode
{
    /// <summary>
    /// The art style and palette are locked by the owner (Documentation/ArtStyle.md). These fingerprints change
    /// whenever a colour or the way anything is drawn changes, so no change slips in unnoticed. If one fails, the
    /// change needs the owner's approval; once approved, update ArtStyle.md and put the new value here.
    /// </summary>
    public class ArtStyleTests
    {
        private const string Locked = "The art style is locked (see Documentation/ArtStyle.md). Change it only with the owner's approval, then update this fingerprint.";

        private static readonly TownWorld World = StarterTown.CreateWorld();

        [Test]
        public void Palette_IsLocked()
        {
            var entries = Palette.Entries().OrderBy(e => e.name, StringComparer.Ordinal).ToList();
            Assert.That(entries.Select(e => e.name).Distinct().Count(), Is.EqualTo(entries.Count), "every colour has its own name");
            var hash = new Fingerprint();
            foreach (var (name, colour) in entries)
            {
                hash.Add(name);
                hash.Add(colour);
            }

            Assert.That(entries.Count, Is.EqualTo(271), Locked);
            Assert.That(hash.Value, Is.EqualTo(11191823668855753653UL), Locked);
        }

        [Test]
        public void StyleRules_AreLocked()
        {
            Assert.That(IsoPainter.HalfTileWidth, Is.EqualTo(32), Locked);
            Assert.That(IsoPainter.HalfTileHeight, Is.EqualTo(16), Locked);
            Assert.That(IsoPainter.TopLight, Is.EqualTo(1.0), Locked);
            Assert.That(IsoPainter.LeftLight, Is.EqualTo(0.84), Locked);
            Assert.That(IsoPainter.RightLight, Is.EqualTo(0.68), Locked);
            Assert.That(GroundArt.BaseDepth, Is.EqualTo(10), Locked);
            Assert.That(InteriorArt.BackHeight, Is.EqualTo(64), Locked);
            Assert.That(InteriorArt.FrontHeight, Is.EqualTo(12), Locked);
            Assert.That(InteriorArt.InnerHeight, Is.EqualTo(40), Locked);
            Assert.That(Lighting.Twilight, Is.EqualTo(90), Locked);
        }

        [Test]
        public void Ground_IsLocked()
        {
            var hash = new Fingerprint();
            foreach (var season in Seasons())
            {
                foreach (var map in World.Maps)
                {
                    hash.Add(GroundArt.Render(map, PlaceStyle.Of(World, map), Palette.For(season)));
                }
            }

            Assert.That(hash.Value, Is.EqualTo(16077229177817136818UL), Locked);
        }

        [Test]
        public void Buildings_AreLocked()
        {
            World.TryGetMap(StarterTown.VillageMapId, out var village);
            var hash = new Fingerprint();
            foreach (var season in Seasons())
            {
                foreach (var plan in BuildingArt.Find(World, village))
                {
                    var (body, glow) = BuildingArt.Render(plan, Palette.For(season));
                    hash.Add(body);
                    hash.Add(glow);
                }
            }

            Assert.That(hash.Value, Is.EqualTo(7054617281269187708UL), Locked);
        }

        [Test]
        public void RoomWalls_AreLocked()
        {
            var hash = new Fingerprint();
            foreach (var map in World.Maps.Where(m => !m.IsExterior))
            {
                for (var y = 0; y < map.Height; y++)
                {
                    for (var x = 0; x < map.Width; x++)
                    {
                        var wall = InteriorArt.Render(map, PlaceStyle.Of(World, map), x, y);
                        if (wall != null)
                        {
                            hash.Add(wall);
                        }
                    }
                }
            }

            Assert.That(hash.Value, Is.EqualTo(9408866579387279822UL), Locked);
        }

        [Test]
        public void TreesAndFurniture_AreLocked()
        {
            var trees = new Fingerprint();
            var furniture = new Fingerprint();
            foreach (var season in Seasons())
            {
                var palette = Palette.For(season);
                for (var variant = 0; variant < 20; variant++)
                {
                    trees.Add(NatureArt.Tree(palette, variant));
                }

                foreach (ObjectType type in Enum.GetValues(typeof(ObjectType)))
                {
                    furniture.Add(FurnitureArt.Render(type, 1, palette, outdoors: true));
                    furniture.Add(FurnitureArt.Render(type, 2, palette, outdoors: false));
                }

                furniture.Add(FurnitureArt.Render(ObjectType.GatheringSpot, 2, palette, outdoors: false, PlaceKind.Bar));
            }

            Assert.That(trees.Value, Is.EqualTo(16681692374567243500UL), "Trees. " + Locked);
            Assert.That(furniture.Value, Is.EqualTo(2233961772708468399UL), "Furniture. " + Locked);
        }

        [Test]
        public void ResidentsAndMoods_AreLocked()
        {
            var hash = new Fingerprint();
            foreach (var resident in StarterTown.CreateResidents(World))
            {
                var look = ResidentLook.For(resident.Id, resident.AgeYears);
                foreach (var pose in ResidentArtTests.AllPoses())
                {
                    hash.Add(ResidentArt.Render(look, pose));
                }
            }

            foreach (var mood in MoodArt.Shown.OrderBy(m => m))
            {
                hash.Add(MoodArt.Icon(mood));
            }

            Assert.That(hash.Value, Is.EqualTo(8545702447285596364UL), "Residents. " + Locked);
        }

        [Test]
        public void EveryColour_IsNamedInThePalette()
        {
            // Colours are written only in Palette.cs, so the palette really is the whole palette.
            var folder = FindArtFolder();
            if (folder == null)
            {
                Assert.Inconclusive("Couldn't find Assets/Scripts/Art from the test's working folder.");
            }

            foreach (var file in Directory.GetFiles(folder, "*.cs"))
            {
                var name = Path.GetFileName(file);
                if (name == "Palette.cs" || name == "Rgba.cs" || name == "PngReader.cs")
                {
                    continue;
                }

                var text = File.ReadAllText(file);
                Assert.That(text.Contains("Rgba.Hex(") || text.Contains("new Rgba("), Is.False, $"{name} writes a colour of its own; add it to Palette.cs instead.");
            }
        }

        private static Season[] Seasons() => Enum.GetValues(typeof(Season)).Cast<Season>().ToArray();

        private static string FindArtFolder()
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                {
                    var candidate = Path.Combine(dir.FullName, "Assets", "Scripts", "Art");
                    if (Directory.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        /// <summary>FNV-1a over everything added, so any changed pixel or colour changes the value.</summary>
        private sealed class Fingerprint
        {
            public ulong Value { get; private set; } = 14695981039346656037UL;

            public void Add(string text)
            {
                foreach (var c in text)
                {
                    Byte((byte)c);
                    Byte((byte)(c >> 8));
                }
            }

            public void Add(Rgba c)
            {
                Byte(c.R);
                Byte(c.G);
                Byte(c.B);
                Byte(c.A);
            }

            public void Add(PixelCanvas canvas)
            {
                Int(canvas.Width);
                Int(canvas.Height);
                Int((int)Math.Round(canvas.AnchorX * 2));
                Int((int)Math.Round(canvas.AnchorY * 2));
                foreach (var b in canvas.ToBytesBottomUp())
                {
                    Byte(b);
                }
            }

            private void Int(int v)
            {
                Byte((byte)v);
                Byte((byte)(v >> 8));
                Byte((byte)(v >> 16));
                Byte((byte)(v >> 24));
            }

            private void Byte(byte b)
            {
                unchecked
                {
                    Value = (Value ^ b) * 1099511628211UL;
                }
            }
        }
    }
}
