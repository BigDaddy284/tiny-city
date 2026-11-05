using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Economy;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.World;

namespace TinyCity.Simulation.Building
{
    /// <summary>One block of a plan: where on the plot (0, 0 is the plot's back-left corner), how high, and what it's made of.</summary>
    public readonly struct BlockCell
    {
        public BlockCell(int x, int y, int level, MaterialKind material)
        {
            X = x;
            Y = y;
            Level = level;
            Material = material;
        }

        public int X { get; }

        public int Y { get; }

        public int Level { get; }

        public MaterialKind Material { get; }
    }

    /// <summary>A plan question waiting for the AI: which plot it is for. Not saved; after loading they plan again.</summary>
    public sealed class PendingSketch
    {
        public PendingSketch(long id, string plotId, long askedMinute)
        {
            Id = id;
            PlotId = plotId;
            AskedMinute = askedMinute;
        }

        public long Id { get; }

        public string PlotId { get; }

        public long AskedMinute { get; }
    }

    /// <summary>Every number and letter for Tiny City 2's block building.</summary>
    public static class BuildingRules
    {
        /// <summary>A plot is this many tiles each way, behind (north of) its marker.</summary>
        public const int PlotSize = 6;

        /// <summary>Walls are at most this many blocks high.</summary>
        public const int MaxHeight = 3;

        public const int MinBlocks = 4;

        /// <summary>Blocks laid in one go at the plot, if they have the materials.</summary>
        public const int BlocksPerSession = 8;

        public const int MaxNameLength = 40;

        /// <summary>How long a plan question waits for an answer before it is dropped.</summary>
        public const int SketchWaitMinutes = 240;

        /// <summary>Letters a plan uses: one per material, '.' for floor and 'D' for a doorway (both open).</summary>
        public static readonly IReadOnlyDictionary<char, MaterialKind> Letters = new Dictionary<char, MaterialKind>
        {
            ['W'] = MaterialKind.Wood,
            ['S'] = MaterialKind.Stone,
            ['H'] = MaterialKind.Hardwood,
            ['I'] = MaterialKind.Iron,
            ['T'] = MaterialKind.Heartwood,
            ['C'] = MaterialKind.Crystal,
        };

        /// <summary>Tiny City 2's building activities, which don't exist in v1.</summary>
        public static bool IsBuilding(ActivityDefinition a) => a == ActivityCatalog.PlanBuilding || a == ActivityCatalog.Build;

        public static char LetterOf(MaterialKind m) => Letters.First(p => p.Value == m).Key;

        /// <summary>The plot's back-left corner: the plot lies in the <see cref="PlotSize"/> rows behind its marker, two columns to its left and three to its right.</summary>
        public static Location Corner(WorldObject plot) =>
            new Location(plot.Location.MapId, plot.Location.Position.X - 2, plot.Location.Position.Y - PlotSize);

        public static Location TileOf(WorldObject plot, BlockCell cell)
        {
            var c = Corner(plot);
            return new Location(c.MapId, c.Position.X + cell.X, c.Position.Y + cell.Y);
        }

        public static IEnumerable<Location> Footprint(WorldObject plot)
        {
            var c = Corner(plot);
            for (var y = 0; y < PlotSize; y++)
            {
                for (var x = 0; x < PlotSize; x++)
                {
                    yield return new Location(c.MapId, c.Position.X + x, c.Position.Y + y);
                }
            }
        }
    }

    /// <summary>A building plan the game has checked: a name, a wall height, and the rows of its footprint.</summary>
    public sealed class BuildPlan
    {
        private BuildPlan(string name, int height, IReadOnlyList<string> rows, List<BlockCell> cells)
        {
            Name = name;
            Height = height;
            Rows = rows;
            Cells = cells;
        }

        public string Name { get; }

        public int Height { get; }

        /// <summary><see cref="BuildingRules.PlotSize"/> rows of <see cref="BuildingRules.PlotSize"/> letters, back row first.</summary>
        public IReadOnlyList<string> Rows { get; }

        /// <summary>Every block in building order: the whole bottom layer first, then each layer above.</summary>
        public IReadOnlyList<BlockCell> Cells { get; }

        /// <summary>How many of each material the whole plan takes.</summary>
        public IEnumerable<(MaterialKind material, int count)> Needs() =>
            Cells.GroupBy(c => c.Material).OrderBy(g => g.Key).Select(g => (g.Key, g.Count()));

        /// <summary>Checks a plan: the letters, the size, enough blocks, and no room without a way in.</summary>
        public static bool TryParse(string name, int height, IReadOnlyList<string> rows, out BuildPlan plan, out string error)
        {
            plan = null;
            var size = BuildingRules.PlotSize;
            if (height < 1 || height > BuildingRules.MaxHeight)
            {
                error = $"the height must be 1 to {BuildingRules.MaxHeight}";
                return false;
            }

            if (rows == null || rows.Count == 0 || rows.Count > size)
            {
                error = $"the plan needs 1 to {size} rows";
                return false;
            }

            var grid = new char[size, size];
            for (var y = 0; y < size; y++)
            {
                var row = y < rows.Count ? (rows[y] ?? string.Empty).Trim().ToUpperInvariant() : string.Empty;
                if (row.Length > size)
                {
                    error = $"row {y + 1} is longer than {size}";
                    return false;
                }

                for (var x = 0; x < size; x++)
                {
                    var c = x < row.Length ? row[x] : '.';
                    if (c == ' ' || c == '_')
                    {
                        c = '.';
                    }

                    if (c != '.' && c != 'D' && !BuildingRules.Letters.ContainsKey(c))
                    {
                        error = $"'{c}' is not a block letter";
                        return false;
                    }

                    grid[x, y] = c;
                }
            }

            var walls = new List<(int x, int y, MaterialKind m)>();
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    if (BuildingRules.Letters.TryGetValue(grid[x, y], out var m))
                    {
                        walls.Add((x, y, m));
                    }
                }
            }

            if (walls.Count * height < BuildingRules.MinBlocks)
            {
                error = $"a building needs at least {BuildingRules.MinBlocks} blocks";
                return false;
            }

            // Every open tile must be reachable from outside the plot, so nobody is ever shut in.
            var reached = new bool[size + 2, size + 2];
            var queue = new Queue<(int x, int y)>();
            reached[0, 0] = true;
            queue.Enqueue((0, 0));
            while (queue.Count > 0)
            {
                var (px, py) = queue.Dequeue();
                foreach (var (dx, dy) in new[] { (0, -1), (1, 0), (0, 1), (-1, 0) })
                {
                    int nx = px + dx, ny = py + dy;
                    if (nx < 0 || ny < 0 || nx > size + 1 || ny > size + 1 || reached[nx, ny])
                    {
                        continue;
                    }

                    var inside = nx >= 1 && ny >= 1 && nx <= size && ny <= size;
                    if (inside && BuildingRules.Letters.ContainsKey(grid[nx - 1, ny - 1]))
                    {
                        continue;
                    }

                    reached[nx, ny] = true;
                    queue.Enqueue((nx, ny));
                }
            }

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    if (!BuildingRules.Letters.ContainsKey(grid[x, y]) && !reached[x + 1, y + 1])
                    {
                        error = "part of it is closed in with no doorway";
                        return false;
                    }
                }
            }

            var cells = new List<BlockCell>();
            for (var level = 0; level < height; level++)
            {
                cells.AddRange(walls.Select(w => new BlockCell(w.x, w.y, level, w.m)));
            }

            var cleanRows = Enumerable.Range(0, size).Select(y => new string(Enumerable.Range(0, size).Select(x => grid[x, y]).ToArray())).ToList();
            var cleanName = TextRules.Clean(name, BuildingRules.MaxNameLength);
            plan = new BuildPlan(cleanName.Length > 0 ? cleanName : "building", height, cleanRows, cells);
            error = null;
            return true;
        }
    }

    /// <summary>A building someone has planned on a plot, and how many of its blocks are down.</summary>
    public sealed class Project
    {
        public Project(string plotId, string ownerId, BuildPlan plan, int placed, int startedDay)
        {
            PlotId = plotId;
            OwnerId = ownerId;
            Plan = plan;
            Placed = Math.Max(0, Math.Min(placed, plan.Cells.Count));
            StartedDay = startedDay;
        }

        public string PlotId { get; }

        public string OwnerId { get; }

        public BuildPlan Plan { get; }

        public int Placed { get; internal set; }

        public int StartedDay { get; }

        public bool IsFinished => Placed >= Plan.Cells.Count;

        /// <summary>The next block to lay, or null once finished.</summary>
        public BlockCell? Next => IsFinished ? (BlockCell?)null : Plan.Cells[Placed];

        public IEnumerable<BlockCell> PlacedCells => Plan.Cells.Take(Placed);

        /// <summary>Materials still needed to finish.</summary>
        public IEnumerable<(MaterialKind material, int count)> StillNeeded() =>
            Plan.Cells.Skip(Placed).GroupBy(c => c.Material).OrderBy(g => g.Key).Select(g => (g.Key, g.Count()));
    }

    /// <summary>
    /// Tiny City 2's buildings: the project on each plot and the tiles their blocks stand on, which nobody walks through.
    /// Saved with the town; <see cref="Version"/> changes whenever a block is laid, so the view and paths can catch up.
    /// </summary>
    public sealed class Construction
    {
        private readonly Dictionary<string, Project> projects = new Dictionary<string, Project>(StringComparer.Ordinal);
        private readonly HashSet<Location> blocked = new HashSet<Location>();
        private readonly TownWorld world;

        public Construction(TownWorld world)
        {
            this.world = world;
        }

        public int Version { get; private set; }

        public IReadOnlyDictionary<string, Project> Projects => projects;

        public Project At(string plotId) => plotId != null && projects.TryGetValue(plotId, out var p) ? p : null;

        public Project Of(string residentId) => projects.Values.Where(p => p.OwnerId == residentId).OrderBy(p => p.PlotId, StringComparer.Ordinal).FirstOrDefault();

        public bool IsBlocked(Location location) => blocked.Contains(location);

        internal void Add(Project project)
        {
            projects[project.PlotId] = project;
            Rebuild();
        }

        /// <summary>Lays the project's next blocks, up to <paramref name="max"/>, while the builder has the materials and nobody stands in the way.</summary>
        internal List<BlockCell> LayBlocks(Project project, Belongings belongings, int max, Func<Location, bool> occupied, out string stoppedBy)
        {
            var laid = new List<BlockCell>();
            stoppedBy = null;
            if (!world.TryGetObject(project.PlotId, out var plot))
            {
                stoppedBy = "the plot is gone";
                return laid;
            }

            while (laid.Count < max && project.Next is BlockCell next)
            {
                var tile = BuildingRules.TileOf(plot, next);
                if (next.Level == 0 && occupied(tile))
                {
                    stoppedBy = "someone was standing in the way";
                    break;
                }

                if (!belongings.Take(next.Material, 1))
                {
                    stoppedBy = $"ran out of {CraftingRules.Name(next.Material)}";
                    break;
                }

                project.Placed++;
                laid.Add(next);
            }

            if (laid.Count > 0)
            {
                Rebuild();
            }

            return laid;
        }

        private void Rebuild()
        {
            blocked.Clear();
            foreach (var p in projects.Values)
            {
                if (!world.TryGetObject(p.PlotId, out var plot))
                {
                    continue;
                }

                foreach (var cell in p.PlacedCells)
                {
                    blocked.Add(BuildingRules.TileOf(plot, cell));
                }
            }

            Version++;
        }
    }
}
