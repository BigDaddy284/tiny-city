using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;
using TinyCity.Simulation.World;

namespace TinyCity.Simulation.Content
{
    /// <summary>
    /// Tiny City 2's world (owner, 5 November): no city, one flat stretch of open land, laid out as the owner drew it.
    /// Seen from above, the cross of paths splits it into four. The bottom quarter is the starter camp (camp beds,
    /// stoves, benches, trading stalls and crafting benches). The left quarter has trees with fallen branches under
    /// them along its far edge, rocks with loose stones in its corner, a patch of good soil for crops by the cross
    /// path and a pond for fishing; the ground between them is left open where a sawmill or a stone mason's could go,
    /// if anyone builds one. The top and right quarters are open land. Residents plant and build wherever they
    /// choose. They keep their looks, personalities and hobbies from <see cref="StarterTown"/>, but have no job,
    /// rent or business: everything they have, they make.
    /// </summary>
    public static class FlatLand
    {
        public const string TownId = "town-tiny-city-2";
        public const string MapId = "map-flat-land";
        public const string CampBedPrefix = "obj-flat-camp-bed-";

        public const int Width = 56;
        public const int Height = 44;

        /// <summary>The path across the land, and the one down its middle.</summary>
        public const int PathRow = 21;
        public const int PathColumn = 27;

        /// <summary>Camp beds: one for each starter resident and each newcomer who could ever arrive, and a few more.</summary>
        public const int CampBeds = 27;

        private static readonly Dictionary<char, TileType> Legend = new Dictionary<char, TileType>
        {
            ['T'] = TileType.Tree,
            ['.'] = TileType.Grass,
            ['='] = TileType.Path,
            ['~'] = TileType.Water,
            ['g'] = TileType.Garden,
        };

        /// <summary>The pond, in the left quarter: these tiles are water.</summary>
        public static bool IsPond(int x, int y) => x >= 20 && x <= 25 && y >= 29 && y <= 34;

        /// <summary>Good soil for crops, beside the cross path. Crops can be planted on any open ground, but this is where the owner drew the field.</summary>
        public static bool IsCropField(int x, int y) => x >= 18 && x <= 26 && y >= 22 && y <= 26;

        public static GameTime StartTime => StarterTown.StartTime;

        public static IReadOnlyList<string> Rows()
        {
            var rows = new List<string>();
            for (var y = 0; y < Height; y++)
            {
                var row = new char[Width];
                for (var x = 0; x < Width; x++)
                {
                    row[x] = x == 0 || y == 0 || x == Width - 1 || y == Height - 1 ? 'T'
                        : IsPond(x, y) ? '~'
                        : IsCropField(x, y) ? 'g'
                        : y == PathRow || x == PathColumn ? '='
                        : '.';
                }

                rows.Add(new string(row));
            }

            return rows;
        }

        public static TownWorld CreateWorld()
        {
            var map = TileMap.FromRows(MapId, "Open land", true, Rows().ToArray(), Legend);
            var objects = new List<WorldObject>();
            Location At(int x, int y) => new Location(MapId, x, y);

            // The camp, in the bottom quarter: three rows of camp beds.
            var n = 0;
            foreach (var y in new[] { 25, 29, 33 })
            {
                for (var x = 30; x <= 54 && n < CampBeds; x += 3)
                {
                    n++;
                    objects.Add(new WorldObject($"{CampBedPrefix}{n}", "Camp bed", ObjectType.Bed, At(x, y)));
                }
            }

            // Below the beds: stoves and trading stalls, then benches to eat at and crafting benches.
            for (var i = 0; i < 3; i++)
            {
                objects.Add(new WorldObject($"obj-flat-stove-{i + 1}", "Camp stove", ObjectType.Kitchen, At(30 + (i * 4), 37)));
                objects.Add(new WorldObject($"obj-flat-stall-{i + 1}", "Trading stall", ObjectType.MarketStall, At(44 + (i * 4), 37)));
            }

            for (var i = 0; i < 4; i++)
            {
                objects.Add(new WorldObject($"obj-flat-bench-{i + 1}", "Camp bench", ObjectType.Bench, At(30 + (i * 4), 40)));
            }

            objects.Add(new WorldObject("obj-flat-crafting-1", "Crafting bench", ObjectType.Workbench, At(48, 40)));
            objects.Add(new WorldObject("obj-flat-crafting-2", "Crafting bench", ObjectType.Workbench, At(52, 40)));

            // Trees to chop along the left quarter's far edge, with fallen branches under them to pick up by hand.
            var trees = new[] { (2, 23), (5, 24), (2, 26), (4, 28), (2, 30), (5, 31), (2, 33), (4, 35) };
            for (var i = 0; i < trees.Length; i++)
            {
                objects.Add(new WorldObject($"obj-flat-trees-{i + 1}", "Trees", ObjectType.Woodland, At(trees[i].Item1, trees[i].Item2)));
            }

            foreach (var (x, y, i) in new[] { (3, 25, 1), (6, 27, 2), (3, 29, 3), (6, 33, 4) })
            {
                objects.Add(new WorldObject($"obj-flat-branches-{i}", "Fallen branches", ObjectType.StickPile, At(x, y)));
            }

            // Rocks in the left corner, with loose stones about them.
            foreach (var (x, y, i) in new[] { (2, 38, 1), (5, 39, 2), (3, 41, 3), (8, 41, 4) })
            {
                objects.Add(new WorldObject($"obj-flat-rocks-{i}", "Rocks", ObjectType.RockPile, At(x, y)));
            }

            foreach (var (x, y, i) in new[] { (4, 37, 1), (7, 38, 2), (6, 41, 3) })
            {
                objects.Add(new WorldObject($"obj-flat-stones-{i}", "Loose stones", ObjectType.LooseStones, At(x, y)));
            }

            // Fishing spots round the pond.
            objects.Add(new WorldObject("obj-flat-fishing-1", "Fishing spot", ObjectType.FishingSpot, At(19, 31)));
            objects.Add(new WorldObject("obj-flat-fishing-2", "Fishing spot", ObjectType.FishingSpot, At(26, 32)));
            objects.Add(new WorldObject("obj-flat-fishing-3", "Fishing spot", ObjectType.FishingSpot, At(22, 35)));

            return new TownWorld(TownId, "Tiny City", new[] { map }, objects, Array.Empty<Portal>());
        }

        /// <summary>
        /// The starter residents (or those <paramref name="profiles"/> list), each in a camp bed of their own: no home,
        /// job, rent, income or business, only their money and food.
        /// </summary>
        public static IReadOnlyList<Resident> CreateResidents(TownWorld world, IReadOnlyList<ResidentProfile> profiles)
        {
            var residents = StarterTown.CreateResidents(StarterTown.CreateWorld(), profiles);
            var beds = world.ObjectsOfType(ObjectType.Bed).OrderBy(o => o.Id, StringComparer.Ordinal).ToList();
            for (var i = 0; i < residents.Count && i < beds.Count; i++)
            {
                var r = residents[i];
                r.HomeMapId = MapId;
                r.BedObjectId = beds[i].Id;
                r.Location = beds[i].Location;
                r.Job = null;
                r.WeeklyRentPence = 0;
                r.WeeklyIncomePence = 0;
                r.IncomeLabel = string.Empty;
                r.OwnsBusinessId = null;
            }

            return residents.Take(beds.Count).ToList();
        }

        /// <summary>The open land filled to <paramref name="people"/> living residents, for the camera panel test only (never saved).</summary>
        public static TownSimulation CreateCrowdedTown(int people)
        {
            var simulation = CreateNewGame();
            while (simulation.LivingCount < Math.Min(people, PopulationRules.MaxLiving) && simulation.MoveInNewcomer(anyNumber: true) != null)
            {
            }

            return simulation;
        }

        public static TownSimulation CreateNewGame(IReadOnlyList<ResidentProfile> profiles = null)
        {
            var world = CreateWorld();
            var simulation = new TownSimulation(world, CreateResidents(world, profiles), StartTime);
            foreach (var r in simulation.Residents)
            {
                simulation.Memories.Add(r.Id, simulation.Time, MemoryType.LifeHistory, 8,
                    $"I came to this open land with the others to start over. There are no houses or shops here: we sleep in camp beds, and grow, catch and cook our own food, gather wood and stone, make tools and build whatever we like, wherever we like.",
                    tags: new[] { "home", "camp", "building" });
                if (!string.IsNullOrWhiteSpace(r.About))
                {
                    simulation.Memories.Add(r.Id, simulation.Time, MemoryType.LifeHistory, 6, $"Before I came here: {r.About}", tags: new[] { "past" });
                }
            }

            return simulation;
        }
    }
}
