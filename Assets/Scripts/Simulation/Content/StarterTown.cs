using System.Collections.Generic;
using TinyTown.Simulation.Memory;
using TinyTown.Simulation.Residents;
using TinyTown.Simulation.Time;
using TinyTown.Simulation.World;

namespace TinyTown.Simulation.Content
{
    /// <summary>
    /// The Phase 1 town: one small outdoor map, one cottage interior and one resident.
    /// Ids here are stable because save files refer to them.
    /// </summary>
    public static class StarterTown
    {
        public const string TownId = "town-tiny-town";
        public const string VillageMapId = "map-village";
        public const string CottageMapId = "map-cottage-1";
        public const string ResidentId = "res-0001";
        public const string BedId = "obj-cottage1-bed";
        public const string GardenPlotId = "obj-village-garden-plot";

        // T tree, . grass, = path, H cottage wall, D cottage door, ~ pond, g garden bed,
        // G garden plot (work), w garden tap, b park bench, e exercise spot.
        private static readonly string[] VillageRows =
        {
            "TTTTTTTTTTTTTTTTTTTT",
            "T..................T",
            "T.HHHHH.....~~~....T",
            "T.HHHHH.....~~~..e.T",
            "T.HHHHH..b..~~~....T",
            "T.HHDHH............T",
            "T...=..............T",
            "T...============...T",
            "T..............=...T",
            "T.gggGg.w......=...T",
            "T.ggggg........=.b.T",
            "TTTTTTTTTTTTTTTTTTTT",
        };

        // # wall, . floor, D front door, b bed, k kitchen, s sink, t toilet, h shower, a armchair.
        private static readonly string[] CottageRows =
        {
            "#########",
            "#b..#t.h#",
            "#...#...#",
            "#.......#",
            "#k.s..a.#",
            "####D####",
        };

        private static readonly Dictionary<char, TileType> VillageLegend = new Dictionary<char, TileType>
        {
            ['T'] = TileType.Tree,
            ['.'] = TileType.Grass,
            ['='] = TileType.Path,
            ['H'] = TileType.House,
            ['D'] = TileType.Door,
            ['~'] = TileType.Water,
            ['g'] = TileType.Garden,
            ['G'] = TileType.Garden,
            ['w'] = TileType.Grass,
            ['b'] = TileType.Path,
            ['e'] = TileType.Grass,
        };

        private static readonly Dictionary<char, TileType> CottageLegend = new Dictionary<char, TileType>
        {
            ['#'] = TileType.Wall,
            ['.'] = TileType.Floor,
            ['D'] = TileType.Door,
            ['b'] = TileType.Floor,
            ['k'] = TileType.Floor,
            ['s'] = TileType.Floor,
            ['t'] = TileType.Floor,
            ['h'] = TileType.Floor,
            ['a'] = TileType.Floor,
        };

        public static GameTime StartTime => GameTime.FromDayAndTime(1, 7, 0);

        public static TownWorld CreateWorld()
        {
            var village = TileMap.FromRows(VillageMapId, "Tiny Town", true, VillageRows, VillageLegend);
            var cottage = TileMap.FromRows(CottageMapId, "Rose Cottage", false, CottageRows, CottageLegend);

            var objects = new List<WorldObject>();
            AddObjects(objects, VillageMapId, VillageRows, new Dictionary<char, (string id, string name, ObjectType type)>
            {
                ['G'] = (GardenPlotId, "Community garden", ObjectType.GardenPlot),
                ['w'] = ("obj-village-garden-tap", "Garden tap", ObjectType.GardenTap),
                ['e'] = ("obj-village-exercise", "Fitness corner", ObjectType.ExerciseSpot),
            });

            // Two benches share a letter, so give them ids by position.
            objects.Add(new WorldObject("obj-village-bench-pond", "Pond bench", ObjectType.Bench, Find(VillageMapId, VillageRows, 'b', 0)));
            objects.Add(new WorldObject("obj-village-bench-garden", "Garden bench", ObjectType.Bench, Find(VillageMapId, VillageRows, 'b', 1)));

            AddObjects(objects, CottageMapId, CottageRows, new Dictionary<char, (string id, string name, ObjectType type)>
            {
                ['b'] = (BedId, "Bed", ObjectType.Bed),
                ['k'] = ("obj-cottage1-kitchen", "Kitchen", ObjectType.Kitchen),
                ['s'] = ("obj-cottage1-sink", "Sink", ObjectType.Sink),
                ['t'] = ("obj-cottage1-toilet", "Toilet", ObjectType.Toilet),
                ['h'] = ("obj-cottage1-shower", "Shower", ObjectType.Shower),
                ['a'] = ("obj-cottage1-armchair", "Armchair", ObjectType.Armchair),
            });

            var outsideDoor = Find(VillageMapId, VillageRows, 'D', 0);
            var insideDoor = Find(CottageMapId, CottageRows, 'D', 0);
            var portals = new[]
            {
                new Portal("portal-cottage1-out", insideDoor, outsideDoor),
                new Portal("portal-cottage1-in", outsideDoor, insideDoor),
            };

            return new TownWorld(TownId, "The Tiny Town", new[] { village, cottage }, objects, portals);
        }

        public static Resident CreateResident(TownWorld world)
        {
            world.TryGetObject(BedId, out var bed);
            var needs = new Needs(75);
            needs[NeedType.Energy] = 90;
            needs[NeedType.Hunger] = 55;
            needs[NeedType.Toilet] = 50;

            return new Resident(
                ResidentId,
                "Mabel",
                "Thornbury",
                ageYears: 34,
                birthdayDayOfYear: 112,
                colourHex: "#D9734E",
                personality: new Personality(active: 0.7, sociable: 0.6, tidy: 0.65, bookish: 0.55),
                homeMapId: CottageMapId,
                bedObjectId: BedId,
                job: new Job("Community gardener", GardenPlotId, GameTime.MinuteOfDayFor(9, 0), GameTime.MinuteOfDayFor(17, 0), worksWeekends: false),
                location: bed.Location,
                needs: needs);
        }

        public static TownSimulation CreateNewGame()
        {
            var world = CreateWorld();
            var simulation = new TownSimulation(world, new[] { CreateResident(world) }, StartTime);
            AddLifeHistory(simulation);
            return simulation;
        }

        /// <summary>The background each resident starts with, so the AI has something to draw on from the first day.</summary>
        public static void AddLifeHistory(TownSimulation simulation)
        {
            if (simulation.GetResident(ResidentId) == null)
            {
                return;
            }

            var start = simulation.Time;
            simulation.Memories.Add(ResidentId, start, MemoryType.LifeHistory, 7,
                "I moved into Rose Cottage last spring and became the village's community gardener.",
                tags: new[] { CottageMapId, GardenPlotId, "work" });
            simulation.Memories.Add(ResidentId, start, MemoryType.LifeHistory, 5,
                "Reading in my armchair with a cup of tea is my favourite way to unwind.",
                tags: new[] { "obj-cottage1-armchair", "read", "fun" });
        }

        private static void AddObjects(List<WorldObject> objects, string mapId, string[] rows, Dictionary<char, (string id, string name, ObjectType type)> markers)
        {
            foreach (var pair in markers)
            {
                objects.Add(new WorldObject(pair.Value.id, pair.Value.name, pair.Value.type, Find(mapId, rows, pair.Key, 0)));
            }
        }

        private static Location Find(string mapId, string[] rows, char marker, int occurrence)
        {
            var seen = 0;
            for (var y = 0; y < rows.Length; y++)
            {
                for (var x = 0; x < rows[y].Length; x++)
                {
                    if (rows[y][x] == marker && seen++ == occurrence)
                    {
                        return new Location(mapId, x, y);
                    }
                }
            }

            throw new KeyNotFoundException($"Marker '{marker}' #{occurrence} not found on map '{mapId}'.");
        }
    }
}
