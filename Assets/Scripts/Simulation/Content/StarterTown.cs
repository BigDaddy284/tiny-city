using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Community;
using TinyCity.Simulation.Economy;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;
using TinyCity.Simulation.World;

namespace TinyCity.Simulation.Content
{
    /// <summary>
    /// The starter town: a city with sixteen cottages and two apartment towers of eight flats, a general store, café,
    /// bakery, clinic, neon bar, cue club, bowling alley, arcade, gym and market square, and fifteen residents.
    /// Ids here are stable because save files refer to them, so add new content around the existing ids.
    /// </summary>
    public static class StarterTown
    {
        /// <summary>Saves refer to it, so a save can only be loaded into this town.</summary>
        public const string TownId = "town-tiny-city";
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
        public const string RosaId = "res-0004";
        public const string TomId = "res-0005";
        public const string PriyaId = "res-0006";
        public const string IvyId = "res-0007";
        public const string SamuelId = "res-0008";
        public const string FelixId = "res-0009";
        public const string EdithId = "res-0010";
        public const string MarcusId = "res-0011";
        public const string LolaId = "res-0012";
        public const string DannyId = "res-0013";
        public const string RubyId = "res-0014";
        public const string WalterId = "res-0015";
        public const string StoreMapId = "map-store";
        public const string CafeMapId = "map-cafe";
        public const string BakeryMapId = "map-bakery";
        public const string ClinicMapId = "map-clinic";
        public const string StoreId = "biz-store";
        public const string CafeId = "biz-cafe";
        public const string BakeryId = "biz-bakery";
        public const string ClinicId = "biz-clinic";
        public const string BarMapId = "map-bar";
        public const string BarId = "biz-bar";
        public const string BarCounterId = "obj-bar-counter";
        public const string GroceriesItemId = "groceries";
        public const string VillageGreenId = "obj-village-green";
        public const string PondLawnId = "obj-village-pond-lawn";
        public const string QuizCornerId = "obj-cafe-quiz-corner";
        public const string ClubCornerId = "obj-bar-club-corner";
        public const string CueClubMapId = "map-cue-club";
        public const string CueClubId = "biz-cue-club";
        public const string BowlingMapId = "map-bowling";
        public const string BowlingId = "biz-bowling";
        public const string ArcadeMapId = "map-arcade";
        public const string ArcadeId = "biz-arcade";
        public const string GymMapId = "map-gym";
        public const string GymId = "biz-gym";
        public const string MarketSquareId = "obj-village-market-square";
        public const string FountainId = "obj-village-fountain";

        /// <summary>The two apartment towers' flats: "map-flat-a1" to "map-flat-a8" and "map-flat-b1" to "map-flat-b8".</summary>
        public const string FlatMapPrefix = "map-flat-";

        // T tree, . grass, = path, H house wall, ~ pond, g garden bed, G garden plot (work), w garden tap,
        // b/p park bench, e/r exercise spot, W workshop bench (work), a easel, h chess table, f fishing spot,
        // q village green and j pond-side lawn (gathering spots for community events). Doors: D the first three cottages (in reading
        // order), 4-9 and 0 cottages 4-10, S store, C cafe, K bakery, L clinic, N neon bar.
        // The top-left 27 x 15 corner is the Phase 3 village, and the top-left 43 x 27 the town before it grew, both unchanged so
        // saved positions stay valid. The bigger city added: the two apartment towers along the back (U and V, each with eight
        // flat doors, four along the front and four up the right side), Z the six new cottages, P the cue club, O the bowling alley,
        // X the arcade, Y the gym, the market square (M stalls, Q its gathering spot), F the park fountain and n more benches.
        private static readonly string[] VillageRows =
        {
            "TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT",
            "T.............................................HHHHHU...HHHHHV..T",
            "T.HHHHH.....~~~.....HHHHH....HHHHH..HHHHH.....HHHHHU...HHHHHV..T",
            "T.HHHHH.....~~~..e..HHHHH....HHHHH..HHHHH.....HHHHHU...HHHHHV..T",
            "T.HHHHH..b..~~~.....HHHHH....HHHHH..HHHHH.....HHHHHU...HHHHHV..T",
            "T.HHDHH....a........HHDHH....HHSHH..HHCHH.....HHHHHH...HHHHHH..T",
            "T...=.................=........=......=.......UUUUHH...VVVVHH..T",
            "T...======================================.....................T",
            "T..............=.........................=.....................T",
            "T.gggGg.w......=.................ph......======================T",
            "T.ggggg........=.b...........HHHHH..HHHHH=...HHHHHHH..HHHHHHHH.T",
            "T.HHHHH........=.............HHHHH..HHHHH=...HHHHHHH..HHHHHHHH.T",
            "T.HHHHH........=......W......HHHHH..HHHHH=...HHHHHHH..HHHHHHHH.T",
            "T.HHDHH........=.............HHKHH..HHLHH=...HHHPHHH..HHHOHHHH.T",
            "T...===========================================================T",
            "T..............=.........................=.....................T",
            "T..............=........q.........T......=...HHHHHHH..HHHHHHH..T",
            "T.HHHHH..HHHHH.=HHHHH..HHHHH.......HHHHH.=...HHHHHHH..HHHHHHH..T",
            "T.HHHHH..HHHHH.=HHHHH..HHHHH..T....HHHHH.=...HHHHHHH..HHHHHHH..T",
            "T.HHHHH..HHHHH.=HHHHH..HHHHH.......HHHHH.=...HHHXHHH..HHHYHHH..T",
            "T.HH4HH..HH5HH.=HH6HH..HH7HH.......HHNHH.=.....................T",
            "T.=============================================================T",
            "T.HHHHH..HHHHH..HHHHH.=................T.=..T.................TT",
            "T.HHHHH..HHHHH..HHHHH.=.....r...~f.......=...==M==M==M==M==M==.T",
            "T.HHHHH..HHHHH..HHHHH.=........~~~...j...=...=================.T",
            "T.HH8HH..HH9HH..HH0HH.=.............p...T=...=n======Q======n=.T",
            "T.=====================..................=...=================.T",
            "T.....................=..................=.....................T",
            "T.HHHHH..HHHHH.HHHHH..=..T......=.......T=...T.........T.......T",
            "T.HHHHH..HHHHH.HHHHH..=.....T..n=....T...=.....................T",
            "T.HHHHH..HHHHH.HHHHH..=.........=........=.......T..n.......T..T",
            "T.HHZHH..HHZHH.HHZHH..=......n..=........=.....................T",
            "T.==============================F=========.....................T",
            "T.HHHHH..HHHHH.HHHHH..=.........=..n.....==========n===========T",
            "T.HHHHH..HHHHH.HHHHH..=.........=........=.....................T",
            "T.HHHHH..HHHHH.HHHHH..=.....T...=n...T...=.............n.....T.T",
            "T.HHZHH..HHZHH.HHZHH..=.........=........=....T.....T..........T",
            "T.=====================..T......=.......T=................T....T",
            "T........................................=.....................T",
            "TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT",
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

        // x shelves, c counter (work). Furniture blocks walking, so nothing may stand right inside a door or close off a corner.
        private static readonly string[] StoreRows =
        {
            "#########",
            "#x......#",
            "#.......#",
            "#..c....#",
            "####D####",
        };

        // y window and corner tables, m middle and n centre tables (added later, so the first two keep their places),
        // c counter (work), t toilet, s sink, u piano, q quiz corner (gathering spot).
        private static readonly string[] CafeRows =
        {
            "###########",
            "#y.m.y..#t#",
            "#...q...#.#",
            "#u....n...#",
            "#...c...s.#",
            "#####D#####",
        };

        // v display of cakes and pastries, c counter (work).
        private static readonly string[] BakeryRows =
        {
            "#########",
            "#v.....c#",
            "#.......#",
            "####D####",
        };

        // m clinic beds, c desk (work), t toilet, s sink.
        private static readonly string[] ClinicRows =
        {
            "#########",
            "#m.m.#t.#",
            "#....#..#",
            "#.c....s#",
            "####D####",
        };

        // c bar counter (work), o high tables, d and e the 3 x 3 dance floor (d is its first square), q club corner (gathering spot), t toilet, s sink.
        private static readonly string[] BarRows =
        {
            "###########",
            "#...c...#t#",
            "#oeee...#.#",
            "#.eed.q...#",
            "#oeee...s.#",
            "#####D#####",
        };

        // o pool tables, x snooker table, c counter, s sink, t toilet.
        private static readonly string[] CueClubRows =
        {
            "###########",
            "#.........#",
            "#.o..o..x.#",
            "#.........#",
            "#c......st#",
            "#####D#####",
        };

        // l bowling lanes, c counter, s sink, t toilet.
        private static readonly string[] BowlingRows =
        {
            "###########",
            "#.l..l..l.#",
            "#.........#",
            "#.........#",
            "#c.....s.t#",
            "#####D#####",
        };

        // z arcade machines, c change counter, s sink.
        private static readonly string[] ArcadeRows =
        {
            "#########",
            "#z.z.z.z#",
            "#.......#",
            "#c....s.#",
            "####D####",
        };

        // m exercise machines, c front desk, t toilet, s sink, h shower.
        private static readonly string[] GymRows =
        {
            "#########",
            "#m.m.m.t#",
            "#.......#",
            "#c....sh#",
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
            ['p'] = TileType.Path,
            ['e'] = TileType.Grass,
            ['r'] = TileType.Grass,
            ['W'] = TileType.Path,
            ['a'] = TileType.Grass,
            ['h'] = TileType.Path,
            ['f'] = TileType.Grass,
            ['q'] = TileType.Grass,
            ['j'] = TileType.Grass,
            ['S'] = TileType.Door,
            ['C'] = TileType.Door,
            ['K'] = TileType.Door,
            ['L'] = TileType.Door,
            ['N'] = TileType.Door,
            ['U'] = TileType.Door,
            ['V'] = TileType.Door,
            ['Z'] = TileType.Door,
            ['P'] = TileType.Door,
            ['O'] = TileType.Door,
            ['X'] = TileType.Door,
            ['Y'] = TileType.Door,
            ['M'] = TileType.Path,
            ['Q'] = TileType.Path,
            ['F'] = TileType.Path,
            ['n'] = TileType.Path,
            ['0'] = TileType.Door,
            ['1'] = TileType.Door,
            ['2'] = TileType.Door,
            ['3'] = TileType.Door,
            ['4'] = TileType.Door,
            ['5'] = TileType.Door,
            ['6'] = TileType.Door,
            ['7'] = TileType.Door,
            ['8'] = TileType.Door,
            ['9'] = TileType.Door,
        };

        /// <summary>Interiors: walls, doors, and floor under every furniture letter.</summary>
        private static readonly Dictionary<char, TileType> InteriorLegend = new Dictionary<char, TileType>
        {
            ['#'] = TileType.Wall,
            ['D'] = TileType.Door,
        };

        /// <summary>Each cottage: map id, name, object id prefix, and the village door that leads in (marker, occurrence).</summary>
        private static readonly (string mapId, string name, string prefix, char doorMarker, int door)[] Cottages =
        {
            (CottageMapId, "Rose Cottage", "obj-cottage1", 'D', 0),
            (WillowCottageMapId, "Willow Cottage", "obj-cottage2", 'D', 1),
            (FernCottageMapId, "Fern Cottage", "obj-cottage3", 'D', 2),
            ("map-cottage-4", "Bluebell Cottage", "obj-cottage4", '4', 0),
            ("map-cottage-5", "Hawthorn Cottage", "obj-cottage5", '5', 0),
            ("map-cottage-6", "Primrose Cottage", "obj-cottage6", '6', 0),
            ("map-cottage-7", "Oak Cottage", "obj-cottage7", '7', 0),
            ("map-cottage-8", "Heather Cottage", "obj-cottage8", '8', 0),
            ("map-cottage-9", "Clover Cottage", "obj-cottage9", '9', 0),
            ("map-cottage-10", "Juniper Cottage", "obj-cottage10", '0', 0),
            ("map-cottage-11", "Birch Cottage", "obj-cottage11", 'Z', 0),
            ("map-cottage-12", "Elm Cottage", "obj-cottage12", 'Z', 1),
            ("map-cottage-13", "Hazel Cottage", "obj-cottage13", 'Z', 2),
            ("map-cottage-14", "Larch Cottage", "obj-cottage14", 'Z', 3),
            ("map-cottage-15", "Rowan Cottage", "obj-cottage15", 'Z', 4),
            ("map-cottage-16", "Aspen Cottage", "obj-cottage16", 'Z', 5),
        };

        /// <summary>
        /// The flats in the two towers: each tower's eight doors in reading order (four up its right side, then four
        /// along its front), each a home laid out like a cottage.
        /// </summary>
        private static IEnumerable<(string mapId, string name, string prefix, char doorMarker, int door)> Flats()
        {
            foreach (var (letter, marker, tower) in new[] { ('a', 'U', "Skyline Tower"), ('b', 'V', "Aurora Tower") })
            {
                for (var i = 0; i < 8; i++)
                {
                    yield return ($"{FlatMapPrefix}{letter}{i + 1}", $"{tower}, flat {i + 1}", $"obj-flat-{letter}{i + 1}", marker, i);
                }
            }
        }

        public static GameTime StartTime => GameTime.FromDayAndTime(1, 7, 0);

        /// <summary>Monday to Saturday, as a work-day or opening-day mask.</summary>
        private const int MonToSat = 0b0111111;

        /// <summary>Wednesday to Sunday.</summary>
        private const int WedToSun = 0b1111100;

        /// <summary>Tuesday to Sunday.</summary>
        private const int TueToSun = 0b1111110;

        public static TownWorld CreateWorld()
        {
            var maps = new List<TileMap> { TileMap.FromRows(VillageMapId, "Tiny City", true, VillageRows, VillageLegend) };
            var objects = new List<WorldObject>();
            AddObjects(objects, VillageMapId, VillageRows, new Dictionary<char, (string id, string name, ObjectType type)>
            {
                ['G'] = (GardenPlotId, "Community garden", ObjectType.GardenPlot),
                ['w'] = ("obj-village-garden-tap", "Garden tap", ObjectType.GardenTap),
                ['e'] = ("obj-village-exercise", "Fitness corner", ObjectType.ExerciseSpot),
                ['r'] = ("obj-village-fitness-trail", "Fitness trail", ObjectType.ExerciseSpot),
                ['W'] = (WorkbenchId, "Repair workshop", ObjectType.Workbench),
                ['a'] = ("obj-village-easel", "Easel by the pond", ObjectType.Easel),
                ['h'] = ("obj-village-chess", "Chess table", ObjectType.ChessTable),
                ['f'] = ("obj-village-fishing", "Fishing spot", ObjectType.FishingSpot),
                ['q'] = (VillageGreenId, "City plaza", ObjectType.GatheringSpot),
                ['j'] = (PondLawnId, "Pond-side lawn", ObjectType.GatheringSpot),
            });

            // Benches share letters, so give them ids by position.
            objects.Add(new WorldObject("obj-village-bench-pond", "Pond bench", ObjectType.Bench, Find(VillageMapId, VillageRows, 'b', 0)));
            objects.Add(new WorldObject("obj-village-bench-garden", "Garden bench", ObjectType.Bench, Find(VillageMapId, VillageRows, 'b', 1)));
            objects.Add(new WorldObject("obj-village-bench-square", "Square bench", ObjectType.Bench, Find(VillageMapId, VillageRows, 'p', 0)));
            objects.Add(new WorldObject("obj-village-bench-park", "Park bench", ObjectType.Bench, Find(VillageMapId, VillageRows, 'p', 1)));
            AddNewerVillageObjects(objects);

            var portals = new List<Portal>();
            foreach (var cottage in Cottages.Concat(Flats()))
            {
                maps.Add(TileMap.FromRows(cottage.mapId, cottage.name, false, CottageRows, InteriorLegendFor(CottageRows)));
                AddObjects(objects, cottage.mapId, CottageRows, new Dictionary<char, (string id, string name, ObjectType type)>
                {
                    ['b'] = (cottage.prefix + "-bed", "Bed", ObjectType.Bed),
                    ['k'] = (cottage.prefix + "-kitchen", "Kitchen", ObjectType.Kitchen),
                    ['s'] = (cottage.prefix + "-sink", "Sink", ObjectType.Sink),
                    ['t'] = (cottage.prefix + "-toilet", "Toilet", ObjectType.Toilet),
                    ['h'] = (cottage.prefix + "-shower", "Shower", ObjectType.Shower),
                    ['a'] = (cottage.prefix + "-armchair", "Armchair", ObjectType.Armchair),
                });

                AddDoor(portals, cottage.prefix.Substring("obj-".Length), Find(VillageMapId, VillageRows, cottage.doorMarker, cottage.door), Find(cottage.mapId, CottageRows, 'D', 0));
            }

            AddBuilding(maps, objects, portals, StoreMapId, "General store", StoreRows, 'S', "store", new Dictionary<char, (string id, string name, ObjectType type)>
            {
                ['x'] = ("obj-store-shelves", "Shelves", ObjectType.ShopShelf),
                ['c'] = ("obj-store-counter", "Shop counter", ObjectType.Counter),
            });
            AddBuilding(maps, objects, portals, CafeMapId, "Café", CafeRows, 'C', "cafe", new Dictionary<char, (string id, string name, ObjectType type)>
            {
                ['c'] = ("obj-cafe-counter", "Café counter", ObjectType.Counter),
                ['t'] = ("obj-cafe-toilet", "Café toilet", ObjectType.Toilet),
                ['s'] = ("obj-cafe-sink", "Café sink", ObjectType.Sink),
                ['u'] = ("obj-cafe-piano", "Café piano", ObjectType.Piano),
                ['q'] = (QuizCornerId, "Café", ObjectType.GatheringSpot),
            });
            objects.Add(new WorldObject("obj-cafe-table-window", "Window table", ObjectType.CafeTable, Find(CafeMapId, CafeRows, 'y', 0)));
            objects.Add(new WorldObject("obj-cafe-table-corner", "Corner table", ObjectType.CafeTable, Find(CafeMapId, CafeRows, 'y', 1)));
            objects.Add(new WorldObject("obj-cafe-table-middle", "Middle table", ObjectType.CafeTable, Find(CafeMapId, CafeRows, 'm', 0)));
            objects.Add(new WorldObject("obj-cafe-table-centre", "Centre table", ObjectType.CafeTable, Find(CafeMapId, CafeRows, 'n', 0)));
            AddBuilding(maps, objects, portals, BakeryMapId, "Bakery", BakeryRows, 'K', "bakery", new Dictionary<char, (string id, string name, ObjectType type)>
            {
                ['v'] = ("obj-bakery-display", "Cake display", ObjectType.BakeryDisplay),
                ['c'] = ("obj-bakery-counter", "Bakery counter", ObjectType.Counter),
            });
            AddBuilding(maps, objects, portals, ClinicMapId, "Clinic", ClinicRows, 'L', "clinic", new Dictionary<char, (string id, string name, ObjectType type)>
            {
                ['c'] = ("obj-clinic-desk", "Clinic desk", ObjectType.Counter),
                ['t'] = ("obj-clinic-toilet", "Clinic toilet", ObjectType.Toilet),
                ['s'] = ("obj-clinic-sink", "Clinic sink", ObjectType.Sink),
            });
            objects.Add(new WorldObject("obj-clinic-bed-1", "Clinic bed", ObjectType.ClinicBed, Find(ClinicMapId, ClinicRows, 'm', 0)));
            objects.Add(new WorldObject("obj-clinic-bed-2", "Clinic bed", ObjectType.ClinicBed, Find(ClinicMapId, ClinicRows, 'm', 1)));
            AddBuilding(maps, objects, portals, BarMapId, "Neon bar", BarRows, 'N', "bar", new Dictionary<char, (string id, string name, ObjectType type)>
            {
                ['c'] = (BarCounterId, "Bar counter", ObjectType.Counter),
                ['d'] = ("obj-bar-dance-floor", "Dance floor", ObjectType.DanceFloor),
                ['q'] = (ClubCornerId, "Neon bar", ObjectType.GatheringSpot),
                ['t'] = ("obj-bar-toilet", "Bar toilet", ObjectType.Toilet),
                ['s'] = ("obj-bar-sink", "Bar sink", ObjectType.Sink),
            });
            objects.Add(new WorldObject("obj-bar-table-1", "High table", ObjectType.BarTable, Find(BarMapId, BarRows, 'o', 0)));
            objects.Add(new WorldObject("obj-bar-table-2", "Corner table", ObjectType.BarTable, Find(BarMapId, BarRows, 'o', 1)));
            for (var i = 0; i < 8; i++)
            {
                objects.Add(new WorldObject($"obj-bar-dance-floor-{i + 2}", "Dance floor", ObjectType.DanceFloor, Find(BarMapId, BarRows, 'e', i)));
            }

            AddBuilding(maps, objects, portals, CueClubMapId, "Cue club", CueClubRows, 'P', "cue-club", new Dictionary<char, (string id, string name, ObjectType type)>
            {
                ['x'] = ("obj-cue-club-snooker", "Snooker table", ObjectType.SnookerTable),
                ['c'] = ("obj-cue-club-counter", "Club counter", ObjectType.Counter),
                ['s'] = ("obj-cue-club-sink", "Club sink", ObjectType.Sink),
                ['t'] = ("obj-cue-club-toilet", "Club toilet", ObjectType.Toilet),
            });
            objects.Add(new WorldObject("obj-cue-club-pool-1", "Pool table", ObjectType.PoolTable, Find(CueClubMapId, CueClubRows, 'o', 0)));
            objects.Add(new WorldObject("obj-cue-club-pool-2", "Pool table", ObjectType.PoolTable, Find(CueClubMapId, CueClubRows, 'o', 1)));
            AddBuilding(maps, objects, portals, BowlingMapId, "Strike Lanes", BowlingRows, 'O', "bowling", new Dictionary<char, (string id, string name, ObjectType type)>
            {
                ['c'] = ("obj-bowling-counter", "Shoe counter", ObjectType.Counter),
                ['s'] = ("obj-bowling-sink", "Bowling alley sink", ObjectType.Sink),
                ['t'] = ("obj-bowling-toilet", "Bowling alley toilet", ObjectType.Toilet),
            });
            for (var i = 0; i < 3; i++)
            {
                objects.Add(new WorldObject($"obj-bowling-lane-{i + 1}", $"Lane {i + 1}", ObjectType.BowlingLane, Find(BowlingMapId, BowlingRows, 'l', i)));
            }

            AddBuilding(maps, objects, portals, ArcadeMapId, "Pixel Arcade", ArcadeRows, 'X', "arcade", new Dictionary<char, (string id, string name, ObjectType type)>
            {
                ['c'] = ("obj-arcade-counter", "Change counter", ObjectType.Counter),
                ['s'] = ("obj-arcade-sink", "Arcade sink", ObjectType.Sink),
            });
            for (var i = 0; i < 4; i++)
            {
                objects.Add(new WorldObject($"obj-arcade-machine-{i + 1}", "Arcade machine", ObjectType.ArcadeMachine, Find(ArcadeMapId, ArcadeRows, 'z', i)));
            }

            AddBuilding(maps, objects, portals, GymMapId, "Pulse Gym", GymRows, 'Y', "gym", new Dictionary<char, (string id, string name, ObjectType type)>
            {
                ['c'] = ("obj-gym-desk", "Gym desk", ObjectType.Counter),
                ['t'] = ("obj-gym-toilet", "Gym toilet", ObjectType.Toilet),
                ['s'] = ("obj-gym-sink", "Gym sink", ObjectType.Sink),
                ['h'] = ("obj-gym-shower", "Gym shower", ObjectType.Shower),
            });
            for (var i = 0; i < 3; i++)
            {
                objects.Add(new WorldObject($"obj-gym-machine-{i + 1}", "Exercise machine", ObjectType.GymMachine, Find(GymMapId, GymRows, 'm', i)));
            }

            return new TownWorld(TownId, "Tiny City", maps, objects, portals, CreateBusinesses(), new CommunityCalendar(CreateCommunityEvents()), VenueJobs);
        }

        /// <summary>The market square, the fountain and the benches the bigger city added outdoors.</summary>
        private static void AddNewerVillageObjects(List<WorldObject> objects)
        {
            objects.Add(new WorldObject(MarketSquareId, "Market square", ObjectType.GatheringSpot, Find(VillageMapId, VillageRows, 'Q', 0)));
            objects.Add(new WorldObject(FountainId, "Fountain", ObjectType.Fountain, Find(VillageMapId, VillageRows, 'F', 0)));
            var stalls = new[] { "Fruit stall", "Street food stall", "Record stall", "Flower stall", "Gadget stall" };
            for (var i = 0; i < stalls.Length; i++)
            {
                objects.Add(new WorldObject($"obj-village-market-stall-{i + 1}", stalls[i], ObjectType.MarketStall, Find(VillageMapId, VillageRows, 'M', i)));
            }

            for (var i = 0; i < VillageRows.Sum(r => r.Count(c => c == 'n')); i++)
            {
                var at = Find(VillageMapId, VillageRows, 'n', i);
                var name = at.Position.X > 42 ? (at.Position.Y < 27 ? "Market bench" : "Picnic bench") : "Fountain bench";
                objects.Add(new WorldObject($"obj-village-bench-{i + 5}", name, ObjectType.Bench, at));
            }
        }

        /// <summary>The neon bar's evening hours, which are also Felix's shift.</summary>
        private static readonly int BarOpens = GameTime.MinuteOfDayFor(18, 0);
        private static readonly int BarCloses = GameTime.MinuteOfDayFor(23, 30);

        private static IEnumerable<Business> CreateBusinesses()
        {
            yield return new Business(StoreId, "General store", StoreMapId, GameTime.MinuteOfDayFor(8, 0), GameTime.MinuteOfDayFor(17, 0), MonToSat, new[]
            {
                new ShopItem(GroceriesItemId, "Groceries", Money.Pounds(12), 25, groceryMeals: 6),
            });
            yield return new Business(CafeId, "Café", CafeMapId, GameTime.MinuteOfDayFor(8, 0), GameTime.MinuteOfDayFor(20, 0), Job.EveryDay, new[]
            {
                new ShopItem("cafe-meal", "A hot meal", Money.Pounds(7.5), 20),
                new ShopItem("coffee", "Coffee", Money.Pounds(2.8), 40),
            });
            yield return new Business(BakeryId, "Bakery", BakeryMapId, GameTime.MinuteOfDayFor(7, 0), GameTime.MinuteOfDayFor(14, 0), TueToSun, new[]
            {
                new ShopItem("pastry", "Pastry", Money.Pounds(2.4), 24),
            });
            yield return new Business(ClinicId, "Clinic", ClinicMapId, GameTime.MinuteOfDayFor(9, 0), GameTime.MinuteOfDayFor(17, 0), Job.Weekdays, new ShopItem[0]);
            yield return new Business(BarId, "Neon bar", BarMapId, BarOpens, BarCloses, WedToSun, new[]
            {
                new ShopItem("cocktail", "A cocktail", Money.Pounds(5.5), 40),
            }, adultsOnly: true);

            // The venues' hours are the shifts of their jobs (VenueJobs). Until a jobseeker takes one, nobody works
            // there and the venue keeps to its hours; after that it opens only while its worker is in (TownEconomy.IsOpen).
            yield return new Business(CueClubId, "Cue club", CueClubMapId, CueClubJob.StartMinuteOfDay, CueClubJob.EndMinuteOfDay, CueClubJob.WorkDays, new[]
            {
                new ShopItem("pool-game", "A game of pool", Money.Pounds(2), 60),
                new ShopItem("snooker-frame", "A frame of snooker", Money.Pounds(3), 40),
            });
            yield return new Business(BowlingId, "Strike Lanes", BowlingMapId, BowlingJob.StartMinuteOfDay, BowlingJob.EndMinuteOfDay, BowlingJob.WorkDays, new[]
            {
                new ShopItem("bowling-game", "A game of bowling", Money.Pounds(4.5), 60),
            });
            yield return new Business(ArcadeId, "Pixel Arcade", ArcadeMapId, ArcadeJob.StartMinuteOfDay, ArcadeJob.EndMinuteOfDay, ArcadeJob.WorkDays, new[]
            {
                new ShopItem("arcade-credits", "Arcade credits", Money.Pounds(1.5), 80),
            });
            yield return new Business(GymId, "Pulse Gym", GymMapId, GymJob.StartMinuteOfDay, GymJob.EndMinuteOfDay, GymJob.WorkDays, new[]
            {
                new ShopItem("gym-pass", "A day at the gym", Money.Pounds(3.5), 60),
            });
        }

        private static Job CueClubJob => new Job("Cue club host", "obj-cue-club-counter", GameTime.MinuteOfDayFor(16, 0), GameTime.MinuteOfDayFor(23, 30), TueToSun, Money.Pounds(11));

        private static Job BowlingJob => new Job("Lanes attendant", "obj-bowling-counter", GameTime.MinuteOfDayFor(12, 0), GameTime.MinuteOfDayFor(20, 0), TueToSun, Money.Pounds(10.5));

        private static Job ArcadeJob => new Job("Arcade attendant", "obj-arcade-counter", GameTime.MinuteOfDayFor(14, 0), GameTime.MinuteOfDayFor(22, 0), MonToSat, Money.Pounds(10));

        private static Job GymJob => new Job("Gym instructor", "obj-gym-desk", GameTime.MinuteOfDayFor(7, 0), GameTime.MinuteOfDayFor(15, 0), MonToSat, Money.Pounds(11));

        /// <summary>The venues' jobs, held at the start by Marcus, Lola, Danny and Ruby; one that comes free goes to a jobseeker.</summary>
        public static IReadOnlyList<Job> VenueJobs => new[] { GymJob, BowlingJob, ArcadeJob, CueClubJob };

        /// <summary>
        /// The village calendar: a Sunday afternoon get-together by the pond, a Wednesday quiz night at the café, a
        /// Friday club night at the neon bar, a Saturday morning market on the market square, and the village fête once a year (which replaces that Sunday's get-together).
        /// </summary>
        public static IEnumerable<CommunityEvent> CreateCommunityEvents()
        {
            yield return new CommunityEvent("get-together", "Sunday get-together", PondLawnId, Minute(15), Minute(17), days: 1 << (int)Weekday.Sunday, turnout: 0.45);
            yield return new CommunityEvent("quiz-night", "Quiz night", QuizCornerId, Minute(18), Minute(20), days: 1 << (int)Weekday.Wednesday, turnout: 0.4);
            yield return new CommunityEvent("club-night", "Club night", ClubCornerId, Minute(20), Minute(23), days: 1 << (int)Weekday.Friday, turnout: 0.35);
            yield return new CommunityEvent("street-market", "Saturday market", MarketSquareId, Minute(10), Minute(13), days: 1 << (int)Weekday.Saturday, turnout: 0.4);
            yield return new CommunityEvent("village-fete", "City festival", VillageGreenId, Minute(14), Minute(17), yearDay: FeteDayOfYear, turnout: 0.8);
        }

        /// <summary>The fête's day in each 28-day year: the second Sunday.</summary>
        public const int FeteDayOfYear = 14;

        /// <summary>Mabel, the original resident.</summary>
        public static Resident CreateResident(TownWorld world) => CreateResidents(world)[0];

        public static IReadOnlyList<Resident> CreateResidents(TownWorld world)
        {
            var residents = new List<Resident>();
            foreach (var d in Designs)
            {
                var needs = new Needs(d.needsLevel);
                foreach (var pair in d.needAdjustments)
                {
                    needs[pair.Key] = pair.Value;
                }

                var r = new Resident(d.id, d.first, d.last, d.age, d.birthday, d.colour, d.personality, d.homeMapId, d.bedId, d.job, Bed(world, d.bedId), needs);
                ApplyFinances(r, d);
                r.SetHobbies(HobbiesOf(d.id));
                r.OpenToRomance = d.openToRomance;
                residents.Add(r);
            }

            return residents;
        }

        private sealed class Design
        {
            public string id;
            public string first;
            public string last;
            public int age;
            public int birthday;
            public string colour;
            public Personality personality;
            public string homeMapId;
            public string bedId;
            public Job job;
            public double needsLevel;
            public Dictionary<NeedType, double> needAdjustments = new Dictionary<NeedType, double>();
            public long money;
            public int groceries;
            public long rent;
            public long income;
            public string incomeLabel = string.Empty;
            public bool openToRomance = true;
        }

        private static int Minute(int hour, int minute = 0) => GameTime.MinuteOfDayFor(hour, minute);

        /// <summary>Felix runs the neon bar in the evenings, Wednesday to Sunday.</summary>
        private static Job BarJob => new Job("Bar host", BarCounterId, GameTime.MinuteOfDayFor(18, 0), GameTime.MinuteOfDayFor(23, 30), WedToSun, Money.Pounds(11.5));

        private static readonly Design[] Designs =
        {
            new Design
            {
                id = ResidentId, first = "Mabel", last = "Thornbury", age = 34, birthday = 112, colour = "#D9734E",
                personality = new Personality(active: 0.7, sociable: 0.6, tidy: 0.65, bookish: 0.55),
                homeMapId = CottageMapId, bedId = BedId,
                job = new Job("Community gardener", GardenPlotId, Minute(9), Minute(17), Job.Weekdays, Money.Pounds(12)),
                needsLevel = 75, needAdjustments = { [NeedType.Energy] = 90, [NeedType.Hunger] = 55, [NeedType.Toilet] = 50 },
                money = Money.Pounds(450), groceries = 4, rent = Money.Pounds(120),
            },
            new Design
            {
                id = ArthurId, first = "Arthur", last = "Bramble", age = 71, birthday = 290, colour = "#5B7DB1", openToRomance = false,
                personality = new Personality(active: 0.3, sociable: 0.8, tidy: 0.55, bookish: 0.75),
                homeMapId = WillowCottageMapId, bedId = "obj-cottage2-bed",
                needsLevel = 70, needAdjustments = { [NeedType.Energy] = 85, [NeedType.Social] = 45 },
                money = Money.Pounds(800), groceries = 6, rent = Money.Pounds(110), income = Money.Pounds(260), incomeLabel = "Pension",
            },
            new Design
            {
                id = NellId, first = "Nell", last = "Finch", age = 27, birthday = 47, colour = "#6FAE5C",
                personality = new Personality(active: 0.65, sociable: 0.35, tidy: 0.3, bookish: 0.25),
                homeMapId = FernCottageMapId, bedId = "obj-cottage3-bed",
                job = new Job("Repair-maker", WorkbenchId, Minute(10), Minute(18), Job.Weekdays, Money.Pounds(14)),
                needsLevel = 72, needAdjustments = { [NeedType.Energy] = 80, [NeedType.Thirst] = 55 },
                money = Money.Pounds(300), groceries = 2, rent = Money.Pounds(120),
            },
            new Design
            {
                id = RosaId, first = "Rosa", last = "Pennywhistle", age = 46, birthday = 201, colour = "#C2577A",
                personality = new Personality(active: 0.5, sociable: 0.7, tidy: 0.8, bookish: 0.4),
                homeMapId = "map-cottage-4", bedId = "obj-cottage4-bed",
                job = new Job("Shopkeeper", "obj-store-counter", Minute(8), Minute(17), MonToSat, Money.Pounds(13)),
                needsLevel = 74, needAdjustments = { [NeedType.Energy] = 88 },
                money = Money.Pounds(600), groceries = 6, rent = Money.Pounds(120),
            },
            new Design
            {
                id = TomId, first = "Tom", last = "Hartley", age = 31, birthday = 15, colour = "#E0A33A",
                personality = new Personality(active: 0.6, sociable: 0.6, tidy: 0.4, bookish: 0.3),
                homeMapId = "map-cottage-5", bedId = "obj-cottage5-bed",
                job = new Job("Café cook", "obj-cafe-counter", Minute(8), Minute(16), Job.Weekdays, Money.Pounds(11.5)),
                needsLevel = 72, needAdjustments = { [NeedType.Energy] = 82, [NeedType.Hunger] = 60 },
                money = Money.Pounds(350), groceries = 3, rent = Money.Pounds(120),
            },
            new Design
            {
                id = PriyaId, first = "Priya", last = "Lane", age = 38, birthday = 330, colour = "#8E6CC0",
                personality = new Personality(active: 0.55, sociable: 0.85, tidy: 0.5, bookish: 0.6),
                homeMapId = "map-cottage-6", bedId = "obj-cottage6-bed",
                job = new Job("Café server", "obj-cafe-counter", Minute(12), Minute(20), WedToSun, Money.Pounds(11)),
                needsLevel = 76, needAdjustments = { [NeedType.Energy] = 90, [NeedType.Social] = 50 },
                money = Money.Pounds(400), groceries = 5, rent = Money.Pounds(120),
            },
            new Design
            {
                id = IvyId, first = "Ivy", last = "Marsh", age = 23, birthday = 77, colour = "#D4B44A",
                personality = new Personality(active: 0.75, sociable: 0.45, tidy: 0.6, bookish: 0.35),
                homeMapId = "map-cottage-7", bedId = "obj-cottage7-bed",
                job = new Job("Baker", "obj-bakery-counter", Minute(7), Minute(14), TueToSun, Money.Pounds(11.5)),
                needsLevel = 75, needAdjustments = { [NeedType.Energy] = 85 },
                money = Money.Pounds(250), groceries = 4, rent = Money.Pounds(115),
            },
            new Design
            {
                id = SamuelId, first = "Samuel", last = "Okafor", age = 54, birthday = 160, colour = "#3E8E8A",
                personality = new Personality(active: 0.4, sociable: 0.55, tidy: 0.75, bookish: 0.85),
                homeMapId = "map-cottage-8", bedId = "obj-cottage8-bed",
                job = new Job("City doctor", "obj-clinic-desk", Minute(9), Minute(17), Job.Weekdays, Money.Pounds(24)),
                needsLevel = 74, needAdjustments = { [NeedType.Energy] = 88 },
                money = Money.Pounds(1200), groceries = 6, rent = Money.Pounds(130),
            },
            new Design
            {
                id = FelixId, first = "Felix", last = "Wren", age = 19, birthday = 250, colour = "#7FA7D9",
                personality = new Personality(active: 0.7, sociable: 0.5, tidy: 0.2, bookish: 0.5),
                homeMapId = "map-cottage-9", bedId = "obj-cottage9-bed",
                needsLevel = 70, needAdjustments = { [NeedType.Energy] = 75, [NeedType.Fun] = 50 },
                job = BarJob,
                money = Money.Pounds(120), groceries = 2, rent = Money.Pounds(80),
            },
            new Design
            {
                id = EdithId, first = "Edith", last = "Crumb", age = 82, birthday = 5, colour = "#B0A8A0", openToRomance = false,
                personality = new Personality(active: 0.15, sociable: 0.6, tidy: 0.7, bookish: 0.8),
                homeMapId = "map-cottage-10", bedId = "obj-cottage10-bed",
                needsLevel = 72, needAdjustments = { [NeedType.Energy] = 80 },
                money = Money.Pounds(1500), groceries = 5, rent = Money.Pounds(90), income = Money.Pounds(240), incomeLabel = "Pension",
            },

            // The venues' staff and a retired neighbour, who moved in when the city grew.
            new Design
            {
                id = MarcusId, first = "Marcus", last = "Vale", age = 29, birthday = 64, colour = "#4FB38A",
                personality = new Personality(active: 0.9, sociable: 0.6, tidy: 0.65, bookish: 0.25),
                homeMapId = "map-flat-a1", bedId = "obj-flat-a1-bed",
                job = GymJob,
                needsLevel = 76, needAdjustments = { [NeedType.Energy] = 90, [NeedType.Exercise] = 60 },
                money = Money.Pounds(380), groceries = 4, rent = Money.Pounds(100),
            },
            new Design
            {
                id = LolaId, first = "Lola", last = "Brightwater", age = 22, birthday = 183, colour = "#E06AA8",
                personality = new Personality(active: 0.6, sociable: 0.85, tidy: 0.35, bookish: 0.3),
                homeMapId = "map-flat-a2", bedId = "obj-flat-a2-bed",
                job = BowlingJob,
                needsLevel = 74, needAdjustments = { [NeedType.Energy] = 85, [NeedType.Fun] = 55 },
                money = Money.Pounds(180), groceries = 3, rent = Money.Pounds(100),
            },
            new Design
            {
                id = DannyId, first = "Danny", last = "Cho", age = 20, birthday = 238, colour = "#5A8FE0",
                personality = new Personality(active: 0.4, sociable: 0.45, tidy: 0.3, bookish: 0.65),
                homeMapId = "map-flat-a3", bedId = "obj-flat-a3-bed",
                job = ArcadeJob,
                needsLevel = 72, needAdjustments = { [NeedType.Energy] = 80, [NeedType.Social] = 55 },
                money = Money.Pounds(150), groceries = 2, rent = Money.Pounds(100),
            },
            new Design
            {
                id = RubyId, first = "Ruby", last = "Starling", age = 41, birthday = 21, colour = "#C94F4F",
                personality = new Personality(active: 0.45, sociable: 0.75, tidy: 0.6, bookish: 0.45),
                homeMapId = "map-flat-a4", bedId = "obj-flat-a4-bed",
                job = CueClubJob,
                needsLevel = 74, needAdjustments = { [NeedType.Energy] = 85 },
                money = Money.Pounds(520), groceries = 4, rent = Money.Pounds(100),
            },
            new Design
            {
                id = WalterId, first = "Walter", last = "Penn", age = 76, birthday = 309, colour = "#9C8B6E", openToRomance = false,
                personality = new Personality(active: 0.35, sociable: 0.5, tidy: 0.7, bookish: 0.7),
                homeMapId = "map-cottage-11", bedId = "obj-cottage11-bed",
                needsLevel = 72, needAdjustments = { [NeedType.Energy] = 82 },
                money = Money.Pounds(900), groceries = 5, rent = Money.Pounds(120), income = Money.Pounds(250), incomeLabel = "Pension",
            },
        };

        /// <summary>Each starter resident's hobbies, chosen to suit them; shared ones give neighbours something to do together.</summary>
        private static readonly Dictionary<string, string[]> StarterHobbies = new Dictionary<string, string[]>
        {
            [ResidentId] = new[] { "gardening", "painting" },
            [ArthurId] = new[] { "chess", "fishing" },
            [NellId] = new[] { "fishing", "chess" },
            [RosaId] = new[] { "knitting", "baking" },
            [TomId] = new[] { "baking", "piano" },
            [PriyaId] = new[] { "painting", "piano" },
            [IvyId] = new[] { "gardening", "fishing" },
            [SamuelId] = new[] { "chess", "piano" },
            [FelixId] = new[] { "chess", "painting" },
            [EdithId] = new[] { "knitting", "gardening" },
            [MarcusId] = new[] { "fishing", "gardening" },
            [LolaId] = new[] { "painting", "baking" },
            [DannyId] = new[] { "chess", "piano" },
            [RubyId] = new[] { "chess", "knitting" },
            [WalterId] = new[] { "fishing", "gardening" },
        };

        private static IEnumerable<string> HobbiesOf(string residentId) =>
            StarterHobbies.TryGetValue(residentId, out var hobbies) ? hobbies : new string[0];

        private static void ApplyFinances(Resident r, Design d)
        {
            r.MoneyPence = d.money;
            r.Groceries = d.groceries;
            r.WeeklyRentPence = d.rent;
            r.WeeklyIncomePence = d.income;
            r.IncomeLabel = d.incomeLabel;
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
                    yield return (7, "I moved into Rose Cottage last spring and became the city's community gardener.", new[] { CottageMapId, GardenPlotId, "work" });
                    yield return (5, "Reading in my armchair with a cup of tea is my favourite way to unwind.", new[] { "obj-cottage1-armchair", "read", "fun" });
                    break;
                case ArthurId:
                    yield return (7, "I ran the local post office for forty years, so I know everyone's comings and goings.", new[] { WillowCottageMapId, "social" });
                    yield return (5, "Since I retired I miss the chatter at the counter; a good natter on the pond bench helps.", new[] { "obj-village-bench-pond", "watch-town", "chat", "social" });
                    break;
                case NellId:
                    yield return (7, "I moved to Tiny City a month ago to set up the repair workshop.", new[] { FernCottageMapId, WorkbenchId, "work" });
                    yield return (4, "I'd rather fix a squeaky gate than make small talk, but I'm trying to meet people.", new[] { "chat", "social" });
                    break;
                case RosaId:
                    yield return (7, "I took over the general store from my aunt; I know what everyone buys before they ask.", new[] { StoreMapId, "obj-store-counter", "work" });
                    yield return (5, "I like everything in its place: tins lined up, labels facing out.", new[] { "work", "tidy" });
                    break;
                case TomId:
                    yield return (7, "I trained as a chef downtown and came home to cook at the neighbourhood café.", new[] { CafeMapId, "obj-cafe-counter", "work" });
                    yield return (4, "My soup of the day is never the same twice, and I'm proud of that.", new[] { "cafe-meal", "work" });
                    break;
                case PriyaId:
                    yield return (7, "Serving at the café means I hear all the city news first.", new[] { CafeMapId, "work", "social" });
                    yield return (5, "I paint watercolours on my days off and dream of a little exhibition.", new[] { "fun", "art" });
                    break;
                case IvyId:
                    yield return (7, "I'm up before everyone else to bake the neighbourhood's bread; the bakery was my grandmother's.", new[] { BakeryMapId, "obj-bakery-counter", "work" });
                    yield return (4, "I go for a run along the fitness trail whenever my legs aren't covered in flour.", new[] { "obj-village-fitness-trail", "exercise" });
                    break;
                case SamuelId:
                    yield return (7, "I've been the city doctor for twenty years; I've stitched up half the neighbourhood at some point.", new[] { ClinicMapId, "obj-clinic-desk", "work" });
                    yield return (5, "Evenings with a medical journal and a pot of tea suit me fine.", new[] { "read", "fun" });
                    break;
                case FelixId:
                    yield return (7, "I left school last summer, and I've just got my first proper job: running the neon bar in the evenings.", new[] { BarMapId, "money", "work" });
                    yield return (4, "Money's tight, so I make the most of free things: the park, the trail, a chat.", new[] { "money", "exercise", "social" });
                    break;
                case EdithId:
                    yield return (8, "I've lived in Tiny City all my eighty-two years and remember when the pond was a well.", new[] { "obj-village-bench-pond", "social" });
                    yield return (5, "My knees aren't what they were, but a cup of coffee at the café keeps me cheerful.", new[] { CafeMapId, "coffee", "social" });
                    break;
                case MarcusId:
                    yield return (7, "I run the Pulse Gym's early shift; there's nothing like an empty gym at seven in the morning.", new[] { GymMapId, "obj-gym-desk", "work", "exercise" });
                    yield return (4, "I moved into Skyline Tower for the view, and I'm still learning everyone's names.", new[] { "map-flat-a1", "social" });
                    break;
                case LolaId:
                    yield return (7, "I hand out the shoes at Strike Lanes and know every regular's size by heart.", new[] { BowlingMapId, "obj-bowling-counter", "work" });
                    yield return (5, "I love a busy room and a loud laugh; quiet evenings make me restless.", new[] { "social", "fun" });
                    break;
                case DannyId:
                    yield return (7, "I look after the Pixel Arcade, and I hold the high score on two of its machines.", new[] { ArcadeMapId, "obj-arcade-counter", "work" });
                    yield return (4, "I'm better with machines than with people, but I'm working on it.", new[] { "chat", "social" });
                    break;
                case RubyId:
                    yield return (7, "I run the cue club in the evenings; I've played pool since I could see over the table.", new[] { CueClubMapId, "obj-cue-club-counter", "work" });
                    yield return (5, "A good game of snooker settles any argument, as far as I'm concerned.", new[] { "fun", "social" });
                    break;
                case WalterId:
                    yield return (7, "I retired from the railways and moved into Birch Cottage to be near the park.", new[] { "map-cottage-11", "social" });
                    yield return (5, "A morning by the pond with a fishing rod is my idea of a good day.", new[] { "obj-village-bench-pond", "fishing", "fun" });
                    break;
            }
        }

        private static Location Bed(TownWorld world, string bedId)
        {
            world.TryGetObject(bedId, out var bed);
            return bed.Location;
        }

        private static void AddBuilding(List<TileMap> maps, List<WorldObject> objects, List<Portal> portals, string mapId, string name, string[] rows, char doorMarker, string doorName, Dictionary<char, (string id, string name, ObjectType type)> markers)
        {
            maps.Add(TileMap.FromRows(mapId, name, false, rows, InteriorLegendFor(rows)));
            AddObjects(objects, mapId, rows, markers);
            AddDoor(portals, doorName, Find(VillageMapId, VillageRows, doorMarker, 0), Find(mapId, rows, 'D', 0));
        }

        private static void AddDoor(List<Portal> portals, string name, Location outside, Location inside)
        {
            portals.Add(new Portal($"portal-{name}-out", inside, outside));
            portals.Add(new Portal($"portal-{name}-in", outside, inside));
        }

        /// <summary>Every letter in an interior is floor except walls and the door.</summary>
        private static Dictionary<char, TileType> InteriorLegendFor(string[] rows)
        {
            var legend = new Dictionary<char, TileType>(InteriorLegend);
            foreach (var row in rows)
            {
                foreach (var c in row)
                {
                    if (!legend.ContainsKey(c))
                    {
                        legend[c] = TileType.Floor;
                    }
                }
            }

            return legend;
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
