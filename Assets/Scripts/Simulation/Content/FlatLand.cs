using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Building;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;
using TinyCity.Simulation.World;

namespace TinyCity.Simulation.Content
{
    /// <summary>
    /// Tiny City 2's world (owner, 5 November): no city, one flat stretch of open land. It has a starter camp where
    /// everyone has a camp bed, a cooking area with stoves, benches and stalls, a garden, a pond for fishing, a resource
    /// area with woodland, rock piles and crafting benches, and twelve building plots where residents build their own
    /// homes from blocks. Residents keep their looks, personalities and hobbies from <see cref="StarterTown"/>, but have
    /// no job, rent or business: everything they have, they make.
    /// </summary>
    public static class FlatLand
    {
        public const string TownId = "town-tiny-city-2";
        public const string MapId = "map-flat-land";
        public const string CampBedPrefix = "obj-flat-camp-bed-";
        public const string PlotIdPrefix = "obj-flat-plot-";

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

        /// <summary>The pond, top right: these tiles are water.</summary>
        private static bool IsPond(int x, int y) => x >= 45 && x <= 52 && y >= 3 && y <= 9;

        private static bool IsGarden(int x, int y) => x >= 30 && x <= 40 && y >= 3 && y <= 8;

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
                        : IsGarden(x, y) ? 'g'
                        : y == PathRow || x == PathColumn ? '='
                        : '.';
                }

                rows.Add(new string(row));
            }

            return rows;
        }

        /// <summary>The plots' back-left corners: three either side of the middle path, in two rows below the cross path.</summary>
        public static IEnumerable<(int x, int y)> PlotCorners()
        {
            foreach (var y in new[] { 23, 33 })
            {
                foreach (var x in new[] { 1, 10, 19, 28, 37, 46 })
                {
                    yield return (x, y);
                }
            }
        }

        public static TownWorld CreateWorld()
        {
            var map = TileMap.FromRows(MapId, "Open land", true, Rows().ToArray(), Legend);
            var objects = new List<WorldObject>();
            Location At(int x, int y) => new Location(MapId, x, y);

            // The camp, top left: three rows of camp beds.
            var n = 0;
            foreach (var y in new[] { 3, 7, 11 })
            {
                for (var x = 2; x <= 26 && n < CampBeds; x += 3)
                {
                    n++;
                    objects.Add(new WorldObject($"{CampBedPrefix}{n}", "Camp bed", ObjectType.Bed, At(x, y)));
                }
            }

            // The cooking area below it: stoves, benches to eat at and stalls to sell from.
            for (var i = 0; i < 3; i++)
            {
                objects.Add(new WorldObject($"obj-flat-stove-{i + 1}", "Camp stove", ObjectType.Kitchen, At(3 + (i * 4), 15)));
            }

            for (var i = 0; i < 4; i++)
            {
                objects.Add(new WorldObject($"obj-flat-bench-{i + 1}", "Camp bench", ObjectType.Bench, At(3 + (i * 4), 18)));
            }

            for (var i = 0; i < 3; i++)
            {
                objects.Add(new WorldObject($"obj-flat-stall-{i + 1}", "Trading stall", ObjectType.MarketStall, At(18 + (i * 3), 16)));
            }

            // The garden and the pond, top right.
            n = 0;
            foreach (var y in new[] { 4, 7 })
            {
                foreach (var x in new[] { 31, 34, 37, 40 })
                {
                    n++;
                    objects.Add(new WorldObject($"obj-flat-garden-{n}", $"Garden plot {n}", ObjectType.GardenPlot, At(x, y)));
                }
            }

            objects.Add(new WorldObject("obj-flat-fishing-1", "Fishing spot", ObjectType.FishingSpot, At(44, 5)));
            objects.Add(new WorldObject("obj-flat-fishing-2", "Fishing spot", ObjectType.FishingSpot, At(48, 10)));
            objects.Add(new WorldObject("obj-flat-fishing-3", "Fishing spot", ObjectType.FishingSpot, At(53, 7)));

            // The resource area, between the garden and the cross path.
            foreach (var (x, y, i) in new[] { (30, 13, 1), (33, 16, 2), (36, 13, 3), (39, 16, 4) })
            {
                objects.Add(new WorldObject($"obj-flat-woodland-{i}", "Woodland", ObjectType.Woodland, At(x, y)));
            }

            foreach (var (x, y, i) in new[] { (44, 13, 1), (47, 16, 2), (50, 13, 3), (53, 16, 4) })
            {
                objects.Add(new WorldObject($"obj-flat-rocks-{i}", "Rock pile", ObjectType.RockPile, At(x, y)));
            }

            objects.Add(new WorldObject("obj-flat-crafting-1", "Crafting bench", ObjectType.Workbench, At(41, 18)));
            objects.Add(new WorldObject("obj-flat-crafting-2", "Crafting bench", ObjectType.Workbench, At(43, 18)));

            // The building plots, each with its marker in front.
            n = 0;
            foreach (var (x, y) in PlotCorners())
            {
                n++;
                objects.Add(new WorldObject($"{PlotIdPrefix}{n}", $"Plot {n}", ObjectType.BuildPlot, At(x + 3, y + BuildingRules.PlotSize)));
            }

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
                    $"I came to this open land with the others to start over. There are no houses or shops here: we sleep in camp beds, grow, catch and cook our own food, gather wood and stone, make tools, and can build homes of our own.",
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
