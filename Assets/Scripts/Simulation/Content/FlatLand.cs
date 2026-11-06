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
    /// Seen from above, the cross of paths splits it into four. The bottom quarter is the starter camp (owner, 5 November:
    /// no beds and no trading stalls, one stove, four benches and two crafting benches; anything more, residents build). The left quarter has trees with fallen branches under
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

        /// <summary>The most people there can be: twenty start, and the world never grows.</summary>
        public const int MaxResidents = 20;

        public const int Width = 56;
        public const int Height = 44;

        /// <summary>The path across the land, and the one down its middle.</summary>
        public const int PathRow = 21;
        public const int PathColumn = 27;

        /// <summary>Where the residents are standing when the world begins: the camp, a few paces apart.</summary>
        public static IEnumerable<Location> StartingSpots()
        {
            foreach (var y in new[] { 25, 29, 33 })
            {
                for (var x = 30; x <= 54; x += 3)
                {
                    yield return new Location(MapId, x, y);
                }
            }
        }

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

        /// <summary>The meeting circle's two spots in the camp.</summary>
        public static readonly string[] MeetingCircleIds = { "obj-flat-meeting-1", "obj-flat-meeting-2" };

        /// <summary>The first weekly town meeting is a week after the start (owner, 5 November: there is no first-day meeting).</summary>
        public const int DaysToFirstMeeting = 7;

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

            // The camp, in the bottom quarter: one stove, benches to eat at and crafting benches.
            objects.Add(new WorldObject("obj-flat-stove-1", "Camp stove", ObjectType.Kitchen, At(30, 37)));
            for (var i = 0; i < 4; i++)
            {
                objects.Add(new WorldObject($"obj-flat-bench-{i + 1}", "Camp bench", ObjectType.Bench, At(30 + (i * 4), 40)));
            }

            objects.Add(new WorldObject("obj-flat-crafting-1", "Crafting bench", ObjectType.Workbench, At(48, 40)));
            objects.Add(new WorldObject("obj-flat-crafting-2", "Crafting bench", ObjectType.Workbench, At(52, 40)));

            // The meeting circle, where everyone meets once a week (two spots, so all twenty fit).
            objects.Add(new WorldObject(MeetingCircleIds[0], "Meeting circle", ObjectType.GatheringSpot, At(40, 31)));
            objects.Add(new WorldObject(MeetingCircleIds[1], "Meeting circle", ObjectType.GatheringSpot, At(43, 31)));

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
        /// The starter residents (or those <paramref name="profiles"/> list), standing at the camp: no home, bed,
        /// job, rent, income, business or money (unless a profile gives some), only their food.
        /// </summary>
        public static IReadOnlyList<Resident> CreateResidents(TownWorld world, IReadOnlyList<ResidentProfile> profiles)
        {
            var residents = StarterTown.CreateResidents(StarterTown.CreateWorld(), profiles, openLand: true);
            var spots = StartingSpots().ToList();
            for (var i = 0; i < residents.Count && i < spots.Count; i++)
            {
                var r = residents[i];
                r.HomeMapId = null;
                r.BedObjectId = null;
                r.Location = spots[i];
                r.Job = null;
                r.WeeklyRentPence = 0;
                r.WeeklyIncomePence = 0;
                r.IncomeLabel = string.Empty;
                r.OwnsBusinessId = null;

                // Owner, 6 November: nobody arrives with food; v1's starting groceries would become cooked meals.
                r.Groceries = 0;
                // Owner, 6 November: there is no money in Tiny City 2 at all.
                r.MoneyPence = 0;
            }

            return residents.Take(spots.Count).ToList();
        }

        /// <summary>
        /// A new world on the open land. <paramref name="lessons"/> are what an earlier world that died out left behind
        /// (<see cref="History.Legacy.ReadLessons"/>): every resident starts with each as a memory.
        /// </summary>
        public static TownSimulation CreateNewGame(IReadOnlyList<ResidentProfile> profiles = null, IReadOnlyList<string> lessons = null, WorldBrief brief = null)
        {
            var world = CreateWorld();
            var simulation = new TownSimulation(world, CreateResidents(world, profiles), StartTime) { Brief = brief ?? WorldBrief.Default, FirstMeetingDay = StartTime.Day + DaysToFirstMeeting };
            foreach (var r in simulation.Residents)
            {
                simulation.Memories.Add(r.Id, simulation.Time, MemoryType.LifeHistory, 8,
                    $"I came to this open land with the others to start over. There are no houses, beds or shops here, only a camp stove, a few benches and two crafting benches. We sleep wherever we can, grow, catch and cook our own food, gather wood and stone, make tools and build whatever we like, wherever we like.",
                    tags: new[] { "home", "camp", "building" });
                if (!string.IsNullOrWhiteSpace(r.About))
                {
                    simulation.Memories.Add(r.Id, simulation.Time, MemoryType.LifeHistory, 6, $"Before I came here: {r.About}", tags: new[] { "past" });
                }

                foreach (var lesson in lessons ?? Array.Empty<string>())
                {
                    simulation.Memories.Add(r.Id, simulation.Time, MemoryType.LifeHistory, 7, $"Left by those who lived here before us: {lesson}", tags: new[] { "legacy", "past" });
                }
            }

            return simulation;
        }
    }
}
