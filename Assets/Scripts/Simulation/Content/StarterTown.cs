using System.Collections.Generic;
using TinyTown.Simulation.Memory;
using TinyTown.Simulation.Residents;
using TinyTown.Simulation.Time;
using TinyTown.Simulation.World;

namespace TinyTown.Simulation.Content
{
    /// <summary>
    /// The starter town: a small village, three cottages and three residents. Ids here are stable because save
    /// files refer to them; new content is added around existing ids so older saves still fit.
    /// </summary>
    public static class StarterTown
    {
        public const string TownId = "town-tiny-town";
        public const string VillageMapId = "map-village";
        public const string CottageMapId = "map-cottage-1";
        public const string WillowCottageMapId = "map-cottage-2";
        public const string FernCottageMapId = "map-cottage-3";
        public const string ResidentId = "res-0001";
        public const string ArthurId = "res-0002";
        public const string NellId = "res-0003";
        public const string BedId = "obj-cottage1-bed";
        public const string GardenPlotId = "obj-village-garden-plot";
        public const string WorkbenchId = "obj-village-workbench";

        // T tree, . grass, = path, H cottage wall, D cottage door, ~ pond, g garden bed,
        // G garden plot (work), w garden tap, b park bench, e exercise spot, W workshop bench (work).
        // The top-left 20 x 11 corner is the Phase 1 village, unchanged so saved positions stay valid.
        private static readonly string[] VillageRows =
        {
            "TTTTTTTTTTTTTTTTTTTTTTTTTTTT",
            "T..........................T",
            "T.HHHHH.....~~~.....HHHHH..T",
            "T.HHHHH.....~~~..e..HHHHH..T",
            "T.HHHHH..b..~~~.....HHHHH..T",
            "T.HHDHH.............HHDHH..T",
            "T...=.................=....T",
            "T...===================....T",
            "T..............=...........T",
            "T.gggGg.w......=...........T",
            "T.ggggg........=.b.........T",
            "T.HHHHH........=...........T",
            "T.HHHHH........=......W....T",
            "T.HHDHH........=...........T",
            "T...=============..........T",
            "TTTTTTTTTTTTTTTTTTTTTTTTTTTT",
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
            ['W'] = TileType.Path,
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

        /// <summary>Each cottage: map id, name, object id prefix, and which village door (in reading order) leads in.</summary>
        private static readonly (string mapId, string name, string prefix, int door)[] Cottages =
        {
            (CottageMapId, "Rose Cottage", "obj-cottage1", 0),
            (WillowCottageMapId, "Willow Cottage", "obj-cottage2", 1),
            (FernCottageMapId, "Fern Cottage", "obj-cottage3", 2),
        };

        public static GameTime StartTime => GameTime.FromDayAndTime(1, 7, 0);

        public static TownWorld CreateWorld()
        {
            var maps = new List<TileMap> { TileMap.FromRows(VillageMapId, "Tiny Town", true, VillageRows, VillageLegend) };
            var objects = new List<WorldObject>();
            AddObjects(objects, VillageMapId, VillageRows, new Dictionary<char, (string id, string name, ObjectType type)>
            {
                ['G'] = (GardenPlotId, "Community garden", ObjectType.GardenPlot),
                ['w'] = ("obj-village-garden-tap", "Garden tap", ObjectType.GardenTap),
                ['e'] = ("obj-village-exercise", "Fitness corner", ObjectType.ExerciseSpot),
                ['W'] = (WorkbenchId, "Repair workshop", ObjectType.Workbench),
            });

            // Two benches share a letter, so give them ids by position.
            objects.Add(new WorldObject("obj-village-bench-pond", "Pond bench", ObjectType.Bench, Find(VillageMapId, VillageRows, 'b', 0)));
            objects.Add(new WorldObject("obj-village-bench-garden", "Garden bench", ObjectType.Bench, Find(VillageMapId, VillageRows, 'b', 1)));

            var portals = new List<Portal>();
            foreach (var cottage in Cottages)
            {
                maps.Add(TileMap.FromRows(cottage.mapId, cottage.name, false, CottageRows, CottageLegend));
                AddObjects(objects, cottage.mapId, CottageRows, new Dictionary<char, (string id, string name, ObjectType type)>
                {
                    ['b'] = (cottage.prefix + "-bed", "Bed", ObjectType.Bed),
                    ['k'] = (cottage.prefix + "-kitchen", "Kitchen", ObjectType.Kitchen),
                    ['s'] = (cottage.prefix + "-sink", "Sink", ObjectType.Sink),
                    ['t'] = (cottage.prefix + "-toilet", "Toilet", ObjectType.Toilet),
                    ['h'] = (cottage.prefix + "-shower", "Shower", ObjectType.Shower),
                    ['a'] = (cottage.prefix + "-armchair", "Armchair", ObjectType.Armchair),
                });

                var number = cottage.prefix.Substring("obj-cottage".Length);
                var outsideDoor = Find(VillageMapId, VillageRows, 'D', cottage.door);
                var insideDoor = Find(cottage.mapId, CottageRows, 'D', 0);
                portals.Add(new Portal($"portal-cottage{number}-out", insideDoor, outsideDoor));
                portals.Add(new Portal($"portal-cottage{number}-in", outsideDoor, insideDoor));
            }

            return new TownWorld(TownId, "The Tiny Town", maps, objects, portals);
        }

        /// <summary>Mabel, the original resident.</summary>
        public static Resident CreateResident(TownWorld world) => CreateResidents(world)[0];

        public static IReadOnlyList<Resident> CreateResidents(TownWorld world)
        {
            var mabelNeeds = new Needs(75);
            mabelNeeds[NeedType.Energy] = 90;
            mabelNeeds[NeedType.Hunger] = 55;
            mabelNeeds[NeedType.Toilet] = 50;

            var arthurNeeds = new Needs(70);
            arthurNeeds[NeedType.Energy] = 85;
            arthurNeeds[NeedType.Social] = 45;

            var nellNeeds = new Needs(72);
            nellNeeds[NeedType.Energy] = 80;
            nellNeeds[NeedType.Thirst] = 55;

            return new[]
            {
                new Resident(
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
                    location: Bed(world, BedId),
                    needs: mabelNeeds),
                new Resident(
                    ArthurId,
                    "Arthur",
                    "Bramble",
                    ageYears: 71,
                    birthdayDayOfYear: 290,
                    colourHex: "#5B7DB1",
                    personality: new Personality(active: 0.3, sociable: 0.8, tidy: 0.55, bookish: 0.75),
                    homeMapId: WillowCottageMapId,
                    bedObjectId: "obj-cottage2-bed",
                    job: null,
                    location: Bed(world, "obj-cottage2-bed"),
                    needs: arthurNeeds),
                new Resident(
                    NellId,
                    "Nell",
                    "Finch",
                    ageYears: 27,
                    birthdayDayOfYear: 47,
                    colourHex: "#6FAE5C",
                    personality: new Personality(active: 0.65, sociable: 0.35, tidy: 0.3, bookish: 0.25),
                    homeMapId: FernCottageMapId,
                    bedObjectId: "obj-cottage3-bed",
                    job: new Job("Repair-maker", WorkbenchId, GameTime.MinuteOfDayFor(10, 0), GameTime.MinuteOfDayFor(18, 0), worksWeekends: false),
                    location: Bed(world, "obj-cottage3-bed"),
                    needs: nellNeeds),
            };
        }

        public static TownSimulation CreateNewGame()
        {
            var world = CreateWorld();
            var simulation = new TownSimulation(world, CreateResidents(world), StartTime);
            foreach (var resident in simulation.Residents)
            {
                AddLifeHistory(simulation, resident.Id);
            }

            return simulation;
        }

        /// <summary>
        /// Adds any starter resident a loaded save doesn't have yet, such as the Phase 3 newcomers in a Phase 2 save,
        /// at home in bed with their background memories. Returns how many were added.
        /// </summary>
        public static int AddMissingResidents(TownSimulation simulation)
        {
            var added = 0;
            foreach (var resident in CreateResidents(simulation.World))
            {
                if (simulation.GetResident(resident.Id) == null)
                {
                    simulation.AddResident(resident);
                    AddLifeHistory(simulation, resident.Id);
                    added++;
                }
            }

            return added;
        }

        /// <summary>The background each resident starts with, so the AI has something to draw on from the first day.</summary>
        public static void AddLifeHistory(TownSimulation simulation, string residentId = ResidentId)
        {
            if (simulation.GetResident(residentId) == null)
            {
                return;
            }

            var start = simulation.Time;
            foreach (var (importance, summary, tags) in LifeHistory(residentId))
            {
                simulation.Memories.Add(residentId, start, MemoryType.LifeHistory, importance, summary, tags: tags);
            }
        }

        private static IEnumerable<(int importance, string summary, string[] tags)> LifeHistory(string residentId)
        {
            switch (residentId)
            {
                case ResidentId:
                    yield return (7, "I moved into Rose Cottage last spring and became the village's community gardener.", new[] { CottageMapId, GardenPlotId, "work" });
                    yield return (5, "Reading in my armchair with a cup of tea is my favourite way to unwind.", new[] { "obj-cottage1-armchair", "read", "fun" });
                    break;
                case ArthurId:
                    yield return (7, "I ran the village post office for forty years, so I know everyone's comings and goings.", new[] { WillowCottageMapId, "social" });
                    yield return (5, "Since I retired I miss the chatter at the counter; a good natter on the pond bench helps.", new[] { "obj-village-bench-pond", "watch-town", "chat", "social" });
                    break;
                case NellId:
                    yield return (7, "I moved to the Tiny Town a month ago to set up the repair workshop.", new[] { FernCottageMapId, WorkbenchId, "work" });
                    yield return (4, "I'd rather fix a squeaky gate than make small talk, but I'm trying to meet people.", new[] { "chat", "social" });
                    break;
            }
        }

        private static Location Bed(TownWorld world, string bedId)
        {
            world.TryGetObject(bedId, out var bed);
            return bed.Location;
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
