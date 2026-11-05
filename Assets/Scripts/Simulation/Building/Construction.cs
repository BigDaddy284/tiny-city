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
        public const int PlotSize = 8;

        /// <summary>Blocks are a quarter of a tile, the size of a square on the ground: two each way per tile.</summary>
        public const int CellsPerTile = 2;

        /// <summary>A plan is this many letters each way.</summary>
        public const int GridSize = PlotSize * CellsPerTile;

        /// <summary>Walls are at most this many blocks high; four is about a person's height.</summary>
        public const int MaxHeight = 6;

        /// <summary>A doorway is cleared this many blocks high, so anything above it stays as a lintel.</summary>
        public const int DoorHeight = 4;

        public const int MinBlocks = 8;

        /// <summary>One gathered material makes this many blocks.</summary>
        public const int BlocksPerMaterial = 4;

        /// <summary>Blocks laid in one go at the plot, if they have the materials.</summary>
        public const int BlocksPerSession = 32;

        public const int MaxNameLength = 40;

        /// <summary>How long a plan question waits for an answer before it is dropped.</summary>
        public const int SketchWaitMinutes = 240;

        /// <summary>The letter for a bed, which makes a finished building its owner's home.</summary>
        public const char BedLetter = 'B';

        /// <summary>Ids of the furniture a building adds to the world: the plot's id plus this.</summary>
        public const string BedSuffix = "-built-bed";

        /// <summary>Letters a plan uses: one per material, '.' for floor, 'D' for a doorway and 'B' for a bed (all open).</summary>
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

        /// <summary>True for furniture a building put in the world (drawn with the blocks, not with the map).</summary>
        public static bool IsBuilt(WorldObject o) => o != null && o.Id.EndsWith(BedSuffix, StringComparison.Ordinal);

        public static char LetterOf(MaterialKind m) => Letters.First(p => p.Value == m).Key;

        /// <summary>The plot's back-left corner: the plot lies in the <see cref="PlotSize"/> rows behind its marker, three columns to its left and four to its right.</summary>
        public static Location Corner(WorldObject plot) =>
            new Location(plot.Location.MapId, plot.Location.Position.X - 3, plot.Location.Position.Y - PlotSize);

        /// <summary>The tile a block (or any letter of the plan) stands on.</summary>
        public static Location TileOf(WorldObject plot, int cellX, int cellY)
        {
            var c = Corner(plot);
            return new Location(c.MapId, c.Position.X + (cellX / CellsPerTile), c.Position.Y + (cellY / CellsPerTile));
        }

        public static Location TileOf(WorldObject plot, BlockCell cell) => TileOf(plot, cell.X, cell.Y);

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

        /// <summary>Whole materials the first <paramref name="blocks"/> blocks of a plan take: each material comes in lots of <see cref="BlocksPerMaterial"/>.</summary>
        public static Dictionary<MaterialKind, int> MaterialsFor(IEnumerable<BlockCell> blocks) =>
            blocks.GroupBy(c => c.Material).ToDictionary(g => g.Key, g => (g.Count() + BlocksPerMaterial - 1) / BlocksPerMaterial);
    }

    /// <summary>
    /// A building plan the game has checked: a name, a wall height, and <see cref="BuildingRules.GridSize"/> rows of letters,
    /// one per block-sized square of the plot. People stand on whole tiles (2 × 2 letters), so a doorway clears its whole
    /// tile up to <see cref="BuildingRules.DoorHeight"/>, and a bed clears its tile.
    /// </summary>
    public sealed class BuildPlan
    {
        private BuildPlan(string name, int height, IReadOnlyList<string> rows, List<BlockCell> cells, (int x, int y)? bedTile)
        {
            Name = name;
            Height = height;
            Rows = rows;
            Cells = cells;
            BedTile = bedTile;
        }

        public string Name { get; }

        public int Height { get; }

        /// <summary><see cref="BuildingRules.GridSize"/> rows of <see cref="BuildingRules.GridSize"/> letters, back row first.</summary>
        public IReadOnlyList<string> Rows { get; }

        /// <summary>Every block in building order: the whole bottom layer first, then each layer above.</summary>
        public IReadOnlyList<BlockCell> Cells { get; }

        /// <summary>The plot tile (not letter) the bed stands on, or null for a building nobody can live in.</summary>
        public (int x, int y)? BedTile { get; }

        public bool IsHome => BedTile.HasValue;

        /// <summary>How many of each material the whole plan takes.</summary>
        public IEnumerable<(MaterialKind material, int count)> Needs() =>
            BuildingRules.MaterialsFor(Cells).OrderBy(p => p.Key).Select(p => (p.Key, p.Value));

        /// <summary>Checks a plan: the letters, the size, enough blocks, and no room without a way in.</summary>
        public static bool TryParse(string name, int height, IReadOnlyList<string> rows, out BuildPlan plan, out string error)
        {
            plan = null;
            var size = BuildingRules.GridSize;
            var tiles = BuildingRules.PlotSize;
            var per = BuildingRules.CellsPerTile;
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

                    if (c != '.' && c != 'D' && c != BuildingRules.BedLetter && !BuildingRules.Letters.ContainsKey(c))
                    {
                        error = $"'{c}' is not a block letter";
                        return false;
                    }

                    grid[x, y] = c;
                }
            }

            // Doorways and the bed take whole tiles: a doorway clears its tile up to door height, the (first) bed its whole tile.
            var doorTiles = new bool[tiles, tiles];
            (int x, int y)? bed = null;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    if (grid[x, y] == 'D')
                    {
                        doorTiles[x / per, y / per] = true;
                    }
                    else if (grid[x, y] == BuildingRules.BedLetter && bed == null)
                    {
                        bed = (x / per, y / per);
                    }
                }
            }

            int ClearUpTo(int x, int y) =>
                bed is (int bx, int by) && bx == x / per && by == y / per ? height
                : doorTiles[x / per, y / per] ? Math.Min(height, BuildingRules.DoorHeight)
                : 0;

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

            var cells = new List<BlockCell>();
            for (var level = 0; level < height; level++)
            {
                cells.AddRange(walls.Where(w => level >= ClearUpTo(w.x, w.y)).Select(w => new BlockCell(w.x, w.y, level, w.m)));
            }

            if (cells.Count < BuildingRules.MinBlocks)
            {
                error = $"a building needs at least {BuildingRules.MinBlocks} blocks";
                return false;
            }

            // People stand on whole tiles: a tile is open when no block stands on its ground.
            var open = new bool[tiles, tiles];
            for (var ty = 0; ty < tiles; ty++)
            {
                for (var tx = 0; tx < tiles; tx++)
                {
                    open[tx, ty] = true;
                }
            }

            foreach (var c in cells.Where(c => c.Level == 0))
            {
                open[c.X / per, c.Y / per] = false;
            }

            // Every open tile must be reachable from outside the plot, so nobody is ever shut in. The bed is a destination, not a way through.
            var reached = new bool[tiles + 2, tiles + 2];
            var queue = new Queue<(int x, int y)>();
            reached[0, 0] = true;
            queue.Enqueue((0, 0));
            var bedReached = false;
            while (queue.Count > 0)
            {
                var (px, py) = queue.Dequeue();
                foreach (var (dx, dy) in new[] { (0, -1), (1, 0), (0, 1), (-1, 0) })
                {
                    int nx = px + dx, ny = py + dy;
                    if (nx < 0 || ny < 0 || nx > tiles + 1 || ny > tiles + 1 || reached[nx, ny])
                    {
                        continue;
                    }

                    var inside = nx >= 1 && ny >= 1 && nx <= tiles && ny <= tiles;
                    if (inside && bed is (int bx, int by) && bx == nx - 1 && by == ny - 1)
                    {
                        bedReached = true;
                        continue;
                    }

                    if (inside && !open[nx - 1, ny - 1])
                    {
                        continue;
                    }

                    reached[nx, ny] = true;
                    queue.Enqueue((nx, ny));
                }
            }

            for (var y = 0; y < tiles; y++)
            {
                for (var x = 0; x < tiles; x++)
                {
                    var isBed = bed is (int bx, int by) && bx == x && by == y;
                    if (open[x, y] && !isBed && !reached[x + 1, y + 1])
                    {
                        error = "part of it is closed in with no doorway";
                        return false;
                    }
                }
            }

            if (bed != null && !bedReached)
            {
                error = "nobody could get to the bed";
                return false;
            }

            var cleanRows = Enumerable.Range(0, size).Select(y => new string(Enumerable.Range(0, size).Select(x => grid[x, y]).ToArray())).ToList();
            var cleanName = TextRules.Clean(name, BuildingRules.MaxNameLength);
            plan = new BuildPlan(cleanName.Length > 0 ? cleanName : "building", height, cleanRows, cells, bed);
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
        public IEnumerable<(MaterialKind material, int count)> StillNeeded()
        {
            var all = BuildingRules.MaterialsFor(Plan.Cells);
            var used = BuildingRules.MaterialsFor(PlacedCells);
            return all.Select(p => (p.Key, p.Value - (used.TryGetValue(p.Key, out var u) ? u : 0))).Where(p => p.Item2 > 0).OrderBy(p => p.Key);
        }

        /// <summary>
        /// True when laying the next block takes a new material from the builder: every <see cref="BuildingRules.BlocksPerMaterial"/>th
        /// block of a material (counting from the first) does, the ones between use the rest of it.
        /// </summary>
        public bool NextTakesMaterial => Next is BlockCell next && Plan.Cells.Take(Placed).Count(c => c.Material == next.Material) % BuildingRules.BlocksPerMaterial == 0;
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
            world.SetBuilt(Array.Empty<WorldObject>());
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

                if (project.NextTakesMaterial && !belongings.Take(next.Material, 1))
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

        /// <summary>The bed of a finished home on this plot: in the world while the building stands finished.</summary>
        public static string BedIdOf(string plotId) => plotId + BuildingRules.BedSuffix;

        private void Rebuild()
        {
            blocked.Clear();
            var built = new List<WorldObject>();
            foreach (var p in projects.Values.OrderBy(p => p.PlotId, StringComparer.Ordinal))
            {
                if (!world.TryGetObject(p.PlotId, out var plot))
                {
                    continue;
                }

                foreach (var cell in p.PlacedCells.Where(c => c.Level == 0))
                {
                    blocked.Add(BuildingRules.TileOf(plot, cell));
                }

                if (p.IsFinished && p.Plan.BedTile is (int bx, int by))
                {
                    var tile = BuildingRules.TileOf(plot, bx * BuildingRules.CellsPerTile, by * BuildingRules.CellsPerTile);
                    built.Add(new WorldObject(BedIdOf(p.PlotId), $"Bed in the {p.Plan.Name}", ObjectType.Bed, tile));
                }
            }

            world.SetBuilt(built);
            Version++;
        }
    }
}
