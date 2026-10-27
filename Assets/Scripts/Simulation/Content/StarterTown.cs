using System.Collections.Generic;
using TinyTown.Simulation.Economy;
using TinyTown.Simulation.Memory;
using TinyTown.Simulation.Residents;
using TinyTown.Simulation.Time;
using TinyTown.Simulation.World;

namespace TinyTown.Simulation.Content
{
    /// <summary>
    /// The starter town: a village with ten cottages, a general store, café, bakery and clinic, and ten residents.
    /// Ids here are stable because save files refer to them; new content is added around existing ids so older saves
    /// still fit.
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
        public const string RosaId = "res-0004";
        public const string TomId = "res-0005";
        public const string PriyaId = "res-0006";
        public const string IvyId = "res-0007";
        public const string SamuelId = "res-0008";
        public const string FelixId = "res-0009";
        public const string EdithId = "res-0010";
        public const string StoreMapId = "map-store";
        public const string CafeMapId = "map-cafe";
        public const string BakeryMapId = "map-bakery";
        public const string ClinicMapId = "map-clinic";
        public const string StoreId = "biz-store";
        public const string CafeId = "biz-cafe";
        public const string BakeryId = "biz-bakery";
        public const string ClinicId = "biz-clinic";
        public const string GroceriesItemId = "groceries";

        // T tree, . grass, = path, H house wall, ~ pond, g garden bed, G garden plot (work), w garden tap,
        // b/p park bench, e/r exercise spot, W workshop bench (work). Doors: D the first three cottages (in reading
        // order), 4-9 and 0 cottages 4-10, S store, C cafe, K bakery, L clinic.
        // The top-left 27 x 15 corner is the Phase 3 village, unchanged so saved positions stay valid.
        private static readonly string[] VillageRows =
        {
            "TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT",
            "T..........................................T",
            "T.HHHHH.....~~~.....HHHHH....HHHHH..HHHHH..T",
            "T.HHHHH.....~~~..e..HHHHH....HHHHH..HHHHH..T",
            "T.HHHHH..b..~~~.....HHHHH....HHHHH..HHHHH..T",
            "T.HHDHH.............HHDHH....HHSHH..HHCHH..T",
            "T...=.................=........=......=....T",
            "T...======================================.T",
            "T..............=.........................=.T",
            "T.gggGg.w......=.................p.......=.T",
            "T.ggggg........=.b...........HHHHH..HHHHH=.T",
            "T.HHHHH........=.............HHHHH..HHHHH=.T",
            "T.HHHHH........=......W......HHHHH..HHHHH=.T",
            "T.HHDHH........=.............HHKHH..HHLHH=.T",
            "T...======================================.T",
            "T..............=.........................=.T",
            "T..............=..................T......=.T",
            "T.HHHHH..HHHHH.=HHHHH..HHHHH.............=.T",
            "T.HHHHH..HHHHH.=HHHHH..HHHHH..T..........=.T",
            "T.HHHHH..HHHHH.=HHHHH..HHHHH.............=.T",
            "T.HH4HH..HH5HH.=HH6HH..HH7HH.............=.T",
            "T.========================================.T",
            "T.HHHHH..HHHHH..HHHHH.=................T...T",
            "T.HHHHH..HHHHH..HHHHH.=.....r...~..........T",
            "T.HHHHH..HHHHH..HHHHH.=........~~~.........T",
            "T.HH8HH..HH9HH..HH0HH.=.............p...T..T",
            "T.=====================....................T",
            "TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT",
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

        // x shelves, c counter (work).
        private static readonly string[] StoreRows =
        {
            "#########",
            "#x......#",
            "#.......#",
            "#...c...#",
            "####D####",
        };

        // y tables, c counter (work), t toilet, s sink.
        private static readonly string[] CafeRows =
        {
            "###########",
            "#y...y..#t#",
            "#.......#.#",
            "#.........#",
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
            ['S'] = TileType.Door,
            ['C'] = TileType.Door,
            ['K'] = TileType.Door,
            ['L'] = TileType.Door,
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
        };

        public static GameTime StartTime => GameTime.FromDayAndTime(1, 7, 0);

        /// <summary>Monday to Saturday, as a work-day or opening-day mask.</summary>
        private const int MonToSat = 0b0111111;

        /// <summary>Wednesday to Sunday.</summary>
        private const int WedToSun = 0b1111100;

        /// <summary>Tuesday to Sunday.</summary>
        private const int TueToSun = 0b1111110;

        public static TownWorld CreateWorld()
        {
            var maps = new List<TileMap> { TileMap.FromRows(VillageMapId, "Tiny Town", true, VillageRows, VillageLegend) };
            var objects = new List<WorldObject>();
            AddObjects(objects, VillageMapId, VillageRows, new Dictionary<char, (string id, string name, ObjectType type)>
            {
                ['G'] = (GardenPlotId, "Community garden", ObjectType.GardenPlot),
                ['w'] = ("obj-village-garden-tap", "Garden tap", ObjectType.GardenTap),
                ['e'] = ("obj-village-exercise", "Fitness corner", ObjectType.ExerciseSpot),
                ['r'] = ("obj-village-fitness-trail", "Fitness trail", ObjectType.ExerciseSpot),
                ['W'] = (WorkbenchId, "Repair workshop", ObjectType.Workbench),
            });

            // Benches share letters, so give them ids by position.
            objects.Add(new WorldObject("obj-village-bench-pond", "Pond bench", ObjectType.Bench, Find(VillageMapId, VillageRows, 'b', 0)));
            objects.Add(new WorldObject("obj-village-bench-garden", "Garden bench", ObjectType.Bench, Find(VillageMapId, VillageRows, 'b', 1)));
            objects.Add(new WorldObject("obj-village-bench-square", "Village square bench", ObjectType.Bench, Find(VillageMapId, VillageRows, 'p', 0)));
            objects.Add(new WorldObject("obj-village-bench-park", "Park bench", ObjectType.Bench, Find(VillageMapId, VillageRows, 'p', 1)));

            var portals = new List<Portal>();
            foreach (var cottage in Cottages)
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

                var number = cottage.prefix.Substring("obj-cottage".Length);
                AddDoor(portals, $"cottage{number}", Find(VillageMapId, VillageRows, cottage.doorMarker, cottage.door), Find(cottage.mapId, CottageRows, 'D', 0));
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
            });
            objects.Add(new WorldObject("obj-cafe-table-window", "Window table", ObjectType.CafeTable, Find(CafeMapId, CafeRows, 'y', 0)));
            objects.Add(new WorldObject("obj-cafe-table-corner", "Corner table", ObjectType.CafeTable, Find(CafeMapId, CafeRows, 'y', 1)));
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

            return new TownWorld(TownId, "The Tiny Town", maps, objects, portals, CreateBusinesses());
        }

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
        }

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
        }

        private static int Minute(int hour, int minute = 0) => GameTime.MinuteOfDayFor(hour, minute);

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
                id = ArthurId, first = "Arthur", last = "Bramble", age = 71, birthday = 290, colour = "#5B7DB1",
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
                job = new Job("Village doctor", "obj-clinic-desk", Minute(9), Minute(17), Job.Weekdays, Money.Pounds(24)),
                needsLevel = 74, needAdjustments = { [NeedType.Energy] = 88 },
                money = Money.Pounds(1200), groceries = 6, rent = Money.Pounds(130),
            },
            new Design
            {
                id = FelixId, first = "Felix", last = "Wren", age = 19, birthday = 250, colour = "#7FA7D9",
                personality = new Personality(active: 0.7, sociable: 0.5, tidy: 0.2, bookish: 0.5),
                homeMapId = "map-cottage-9", bedId = "obj-cottage9-bed",
                needsLevel = 70, needAdjustments = { [NeedType.Energy] = 75, [NeedType.Fun] = 50 },
                money = Money.Pounds(120), groceries = 2, rent = Money.Pounds(80), income = Money.Pounds(95), incomeLabel = "Jobseeker's allowance",
            },
            new Design
            {
                id = EdithId, first = "Edith", last = "Crumb", age = 82, birthday = 5, colour = "#B0A8A0",
                personality = new Personality(active: 0.15, sociable: 0.6, tidy: 0.7, bookish: 0.8),
                homeMapId = "map-cottage-10", bedId = "obj-cottage10-bed",
                needsLevel = 72, needAdjustments = { [NeedType.Energy] = 80 },
                money = Money.Pounds(1500), groceries = 5, rent = Money.Pounds(90), income = Money.Pounds(240), incomeLabel = "Pension",
            },
        };

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

        /// <summary>
        /// Adds any starter resident a loaded save doesn't have yet, such as newcomers in an older save, at home in
        /// bed with their background memories. Returns how many were added.
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

        /// <summary>
        /// Brings residents from a save older than version 4 into the economy: their job gets its wage and work days,
        /// and they get their starting money, groceries, rent and income. Residents who aren't starter residents keep
        /// what they have.
        /// </summary>
        public static void AddFinancesToOlderSave(TownSimulation simulation)
        {
            foreach (var d in Designs)
            {
                var r = simulation.GetResident(d.id);
                if (r == null)
                {
                    continue;
                }

                if (d.job != null && r.Job != null && r.Job.WorkObjectId == d.job.WorkObjectId)
                {
                    r.Job = d.job;
                }

                ApplyFinances(r, d);
            }
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
                case RosaId:
                    yield return (7, "I took over the general store from my aunt; I know what everyone buys before they ask.", new[] { StoreMapId, "obj-store-counter", "work" });
                    yield return (5, "I like everything in its place: tins lined up, labels facing out.", new[] { "work", "tidy" });
                    break;
                case TomId:
                    yield return (7, "I trained as a chef in the city and came home to cook at the village café.", new[] { CafeMapId, "obj-cafe-counter", "work" });
                    yield return (4, "My soup of the day is never the same twice, and I'm proud of that.", new[] { "cafe-meal", "work" });
                    break;
                case PriyaId:
                    yield return (7, "Serving at the café means I hear all the village news first.", new[] { CafeMapId, "work", "social" });
                    yield return (5, "I paint watercolours on my days off and dream of a little exhibition.", new[] { "fun", "art" });
                    break;
                case IvyId:
                    yield return (7, "I'm up before everyone else to bake the village's bread; the bakery was my grandmother's.", new[] { BakeryMapId, "obj-bakery-counter", "work" });
                    yield return (4, "I go for a run along the fitness trail whenever my legs aren't covered in flour.", new[] { "obj-village-fitness-trail", "exercise" });
                    break;
                case SamuelId:
                    yield return (7, "I've been the village doctor for twenty years; I've stitched up half the town at some point.", new[] { ClinicMapId, "obj-clinic-desk", "work" });
                    yield return (5, "Evenings with a medical journal and a pot of tea suit me fine.", new[] { "read", "fun" });
                    break;
                case FelixId:
                    yield return (7, "I left school last summer and I'm still looking for work in the village.", new[] { "money", "work" });
                    yield return (4, "Money's tight, so I make the most of free things: the park, the trail, a chat.", new[] { "money", "exercise", "social" });
                    break;
                case EdithId:
                    yield return (8, "I've lived in the Tiny Town all my eighty-two years and remember when the pond was a well.", new[] { "obj-village-bench-pond", "social" });
                    yield return (5, "My knees aren't what they were, but a cup of coffee at the café keeps me cheerful.", new[] { CafeMapId, "coffee", "social" });
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
