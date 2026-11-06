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

    /// <summary>A piece of furniture in a plan: the tile it stands on (from the back-left corner), what it is, and its letter.</summary>
    public readonly struct PlanFurniture
    {
        public PlanFurniture(int x, int y, char letter)
        {
            X = x;
            Y = y;
            Letter = letter;
        }

        public int X { get; }

        public int Y { get; }

        public char Letter { get; }

        public ObjectType Type => BuildingRules.Furniture[Letter].type;

        public string Name => BuildingRules.Furniture[Letter].name;
    }

    /// <summary>A plan question waiting for the AI: which building site it is for. Not saved; after loading they plan again.</summary>
    public sealed class PendingSketch
    {
        public PendingSketch(long id, string siteId, long askedMinute)
        {
            Id = id;
            SiteId = siteId;
            AskedMinute = askedMinute;
        }

        public long Id { get; }

        public string SiteId { get; }

        public long AskedMinute { get; }
    }

    /// <summary>Every number and letter for Tiny City 2's block building.</summary>
    public static class BuildingRules
    {
        /// <summary>A building is at most this many tiles each way, so a plan stays small enough for the AI to draw.</summary>
        public const int MaxSize = 8;

        /// <summary>Blocks are a quarter of a tile, the size of a square on the ground: two each way per tile.</summary>
        public const int CellsPerTile = 2;

        /// <summary>A plan is this many letters each way.</summary>
        public const int GridSize = MaxSize * CellsPerTile;

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

        /// <summary>Ids of the furniture a finished building adds to the world: the site's id plus this, then a number.</summary>
        public const string BuiltInfix = "-built-";

        /// <summary>
        /// Furniture a plan can put in (Tiny City 2): each takes the whole tile its letter falls in, and appears once the
        /// building is finished. Anyone can use it; nothing in the world belongs to anyone.
        /// </summary>
        public static readonly IReadOnlyDictionary<char, (ObjectType type, string name)> Furniture = new Dictionary<char, (ObjectType, string)>
        {
            ['K'] = (ObjectType.Kitchen, "stove"),
            ['M'] = (ObjectType.MarketStall, "stall"),
            ['X'] = (ObjectType.Workbench, "crafting bench"),
            ['N'] = (ObjectType.Bench, "bench"),
        };

        /// <summary>Ids of building sites: this plus a number.</summary>
        public const string SitePrefix = "obj-site-";

        /// <summary>Letters a plan uses: one per material, '.' for floor, and 'D' for a doorway (both open).</summary>
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
        public static bool IsBuilt(WorldObject o) => o != null && o.Id.StartsWith(SitePrefix, StringComparison.Ordinal) && o.Id.Contains(BuiltInfix);

        public static char LetterOf(MaterialKind m) => Letters.First(p => p.Value == m).Key;

        /// <summary>The tile a block (or any letter of the plan) stands on, for a building whose back-left corner is <paramref name="corner"/>.</summary>
        public static Location TileOf(Location corner, int cellX, int cellY) =>
            new Location(corner.MapId, corner.Position.X + (cellX / CellsPerTile), corner.Position.Y + (cellY / CellsPerTile));

        public static Location TileOf(Location corner, BlockCell cell) => TileOf(corner, cell.X, cell.Y);

        /// <summary>The tiles a plan covers (its letters' extent), for a building whose back-left corner is <paramref name="corner"/>.</summary>
        public static IEnumerable<Location> Footprint(Location corner, BuildPlan plan)
        {
            for (var y = 0; y < plan.Depth; y++)
            {
                for (var x = 0; x < plan.Width; x++)
                {
                    yield return new Location(corner.MapId, corner.Position.X + x, corner.Position.Y + y);
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
    /// tile up to <see cref="BuildingRules.DoorHeight"/>, and a piece of furniture its tile.
    /// </summary>
    public sealed class BuildPlan
    {
        private BuildPlan(string name, int height, IReadOnlyList<string> rows, List<BlockCell> cells, List<PlanFurniture> furniture, (int x, int y)? doorTile, int width, int depth)
        {
            Furniture = furniture;
            Name = name;
            Height = height;
            Rows = rows;
            Cells = cells;
            DoorTile = doorTile;
            Width = width;
            Depth = depth;
        }

        /// <summary>How many tiles across and deep the plan's letters reach: the ground it takes.</summary>
        public int Width { get; }

        public int Depth { get; }

        /// <summary>The tile of the first doorway, where the builder stands, or null.</summary>
        public (int x, int y)? DoorTile { get; }

        public string Name { get; }

        public int Height { get; }

        /// <summary><see cref="BuildingRules.GridSize"/> rows of <see cref="BuildingRules.GridSize"/> letters, back row first.</summary>
        public IReadOnlyList<string> Rows { get; }

        /// <summary>Every block in building order: the whole bottom layer first, then each layer above.</summary>
        public IReadOnlyList<BlockCell> Cells { get; }

        /// <summary>Every piece of furniture, in plan order (back row first); each has a tile of its own.</summary>
        public IReadOnlyList<PlanFurniture> Furniture { get; }

        /// <summary>How many of each material the whole plan takes.</summary>
        public IEnumerable<(MaterialKind material, int count)> Needs() =>
            BuildingRules.MaterialsFor(Cells).OrderBy(p => p.Key).Select(p => (p.Key, p.Value));

        /// <summary>Checks a plan: the letters, the size, enough blocks, and no room without a way in.</summary>
        public static bool TryParse(string name, int height, IReadOnlyList<string> rows, out BuildPlan plan, out string error)
        {
            plan = null;
            var size = BuildingRules.GridSize;
            var tiles = BuildingRules.MaxSize;
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

                    if (c != '.' && c != 'D' && !BuildingRules.Furniture.ContainsKey(c) && !BuildingRules.Letters.ContainsKey(c))
                    {
                        error = $"'{c}' is not a block letter";
                        return false;
                    }

                    grid[x, y] = c;
                }
            }

            // Doorways and furniture take whole tiles: a doorway clears its tile up to door height, furniture its whole tile
            // (the first furniture letter in a tile is what stands there).
            var doorTiles = new bool[tiles, tiles];
            var furnitureAt = new char[tiles, tiles];
            var furniture = new List<PlanFurniture>();
            (int x, int y)? door = null;
            int width = 0, depth = 0;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    if (grid[x, y] != '.')
                    {
                        width = Math.Max(width, (x / per) + 1);
                        depth = Math.Max(depth, (y / per) + 1);
                    }

                    if (grid[x, y] == 'D')
                    {
                        doorTiles[x / per, y / per] = true;
                        door = door ?? (x / per, y / per);
                    }
                    else if (BuildingRules.Furniture.ContainsKey(grid[x, y]) && furnitureAt[x / per, y / per] == '\0')
                    {
                        furnitureAt[x / per, y / per] = grid[x, y];
                        furniture.Add(new PlanFurniture(x / per, y / per, grid[x, y]));
                    }
                }
            }

            bool IsFurniture(int tx, int ty) => furnitureAt[tx, ty] != '\0';
            int ClearUpTo(int x, int y) =>
                IsFurniture(x / per, y / per) ? height
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

            if (cells.Count < BuildingRules.MinBlocks && furniture.Count == 0)
            {
                error = $"a building needs at least {BuildingRules.MinBlocks} blocks, or some furniture";
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

            // Every open tile must be reachable from outside the plan, so nobody is ever shut in. Furniture is a destination, not a way through.
            var reached = new bool[tiles + 2, tiles + 2];
            var queue = new Queue<(int x, int y)>();
            reached[0, 0] = true;
            queue.Enqueue((0, 0));
            var furnitureReached = new bool[tiles, tiles];
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
                    if (inside && IsFurniture(nx - 1, ny - 1))
                    {
                        furnitureReached[nx - 1, ny - 1] = true;
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
                    if (open[x, y] && !IsFurniture(x, y) && !reached[x + 1, y + 1])
                    {
                        error = "part of it is closed in with no doorway";
                        return false;
                    }
                }
            }

            foreach (var f in furniture.Where(f => !furnitureReached[f.X, f.Y]))
            {
                error = $"nobody could get to the {f.Name}";
                return false;
            }

            var cleanRows = Enumerable.Range(0, size).Select(y => new string(Enumerable.Range(0, size).Select(x => grid[x, y]).ToArray())).ToList();
            var cleanName = TextRules.Clean(name, BuildingRules.MaxNameLength);
            plan = new BuildPlan(cleanName.Length > 0 ? cleanName : "building", height, cleanRows, cells, furniture, door, width, depth);
            error = null;
            return true;
        }
    }

    /// <summary>
    /// A place a resident chose to build on (Tiny City 2): its back-left corner, and where the builder stands, which is
    /// the corner until a plan is accepted and then the first doorway (or the open ground beside the building).
    /// </summary>
    public sealed class Site
    {
        public Site(string id, string ownerId, Location corner, Location standAt)
        {
            Id = id;
            OwnerId = ownerId;
            Corner = corner;
            StandAt = standAt;
        }

        public string Id { get; }

        public string OwnerId { get; }

        public Location Corner { get; }

        public Location StandAt { get; internal set; }
    }

    /// <summary>A building someone has planned on their site, and how many of its blocks are down.</summary>
    public sealed class Project
    {
        public Project(string siteId, string ownerId, Location corner, BuildPlan plan, int placed, int startedDay, IEnumerable<string> builders = null)
        {
            Builders = new SortedSet<string>(builders ?? Array.Empty<string>(), StringComparer.Ordinal);
            SiteId = siteId;
            OwnerId = ownerId;
            Corner = corner;
            Plan = plan;
            Placed = Math.Max(0, Math.Min(placed, plan.Cells.Count));
            StartedDay = startedDay;
        }

        public string SiteId { get; }

        public string OwnerId { get; }

        /// <summary>The tile of the plan's back-left corner.</summary>
        public Location Corner { get; }

        public BuildPlan Plan { get; }

        public int Placed { get; internal set; }

        public int StartedDay { get; }

        /// <summary>Everyone who has laid a block of it: anyone may help, with their own materials. Saved.</summary>
        public SortedSet<string> Builders { get; }

        public bool IsFinished => Placed >= Plan.Cells.Count;

        /// <summary>The next block to lay, or null once finished.</summary>
        public BlockCell? Next => IsFinished ? (BlockCell?)null : Plan.Cells[Placed];

        public IEnumerable<BlockCell> PlacedCells => Plan.Cells.Take(Placed);

        public IEnumerable<Location> Footprint => BuildingRules.Footprint(Corner, Plan);

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
    /// Tiny City 2's buildings: the sites residents chose, the project on each, and the tiles their blocks stand on,
    /// which nobody walks through. Saved with the town; <see cref="Version"/> changes whenever a block is laid or a
    /// site changes, so the view and paths can catch up.
    /// </summary>
    public sealed class Construction
    {
        public const string PlacedSource = "building";

        private readonly SortedDictionary<string, Site> sites = new SortedDictionary<string, Site>(StringComparer.Ordinal);
        private readonly Dictionary<string, Project> projects = new Dictionary<string, Project>(StringComparer.Ordinal);
        private readonly HashSet<Location> blocked = new HashSet<Location>();
        private readonly TownWorld world;

        public Construction(TownWorld world)
        {
            this.world = world;
            world.SetPlaced(PlacedSource, Array.Empty<WorldObject>());
        }

        public int Version { get; private set; }

        /// <summary>The world the buildings stand in, which holds the site markers and finished buildings' furniture.</summary>
        public TownWorld World => world;

        /// <summary>The number the next site's id gets. Saved, so ids are never reused.</summary>
        public int NextSiteNumber { get; internal set; } = 1;

        public IReadOnlyDictionary<string, Project> Projects => projects;

        public IEnumerable<Site> Sites => sites.Values;

        public Site SiteOf(string siteId) => siteId != null && sites.TryGetValue(siteId, out var s) ? s : null;

        public Project At(string siteId) => siteId != null && projects.TryGetValue(siteId, out var p) ? p : null;

        /// <summary>The resident's first building that isn't finished, or else their first one, or null.</summary>
        public Project Of(string residentId) => OwnedBy(residentId).OrderBy(p => p.IsFinished).FirstOrDefault();

        /// <summary>Every building the resident has planned, oldest site first.</summary>
        public IEnumerable<Project> OwnedBy(string residentId) => sites.Values.Where(s => s.OwnerId == residentId).Select(s => At(s.Id)).Where(p => p != null);

        /// <summary>A site the resident chose but hasn't planned yet, or null.</summary>
        public Site UnplannedOf(string residentId) => sites.Values.FirstOrDefault(s => s.OwnerId == residentId && At(s.Id) == null);

        public bool IsBlocked(Location location) => blocked.Contains(location);

        /// <summary>True on ground a building has been planned on, whether or not its blocks are down yet.</summary>
        public bool IsClaimed(Location location) => projects.Values.Any(p => p.Footprint.Contains(location));

        internal Site AddSite(string ownerId, Location spot)
        {
            var site = new Site($"{BuildingRules.SitePrefix}{NextSiteNumber++}", ownerId, spot, spot);
            sites[site.Id] = site;
            Rebuild();
            return site;
        }

        /// <summary>Puts back a saved site (and its project, if it has one).</summary>
        internal void Restore(Site site, Project project)
        {
            sites[site.Id] = site;
            if (project != null)
            {
                projects[site.Id] = project;
            }

            Rebuild();
        }

        internal void RemoveSite(string siteId)
        {
            if (sites.Remove(siteId))
            {
                projects.Remove(siteId);
                Rebuild();
            }
        }

        internal void Add(Project project, Location standAt)
        {
            if (sites.TryGetValue(project.SiteId, out var site))
            {
                site.StandAt = standAt;
            }

            projects[project.SiteId] = project;
            Rebuild();
        }

        /// <summary>
        /// Whether a plan fits with its back-left corner at <paramref name="corner"/>: on open ground of an outdoor map,
        /// clear of furniture, crops and other buildings, and shutting nobody and nothing in once it is built.
        /// Returns what is wrong, or null, with the tile the builder will stand on.
        /// </summary>
        public string CheckPlacement(Location corner, BuildPlan plan, string siteId, Func<Location, bool> taken, Location from, out Location standAt)
        {
            standAt = corner;
            if (!world.TryGetMap(corner.MapId, out var map) || !map.IsExterior)
            {
                return "buildings go outdoors";
            }

            var footprint = new HashSet<Location>(BuildingRules.Footprint(corner, plan));
            foreach (var tile in footprint)
            {
                if (!map.IsWalkable(tile.Position))
                {
                    return $"({tile.Position.X}, {tile.Position.Y}) isn't open ground, so it doesn't fit there";
                }

                if (world.Objects.Any(o => o.Location == tile && o.Id != siteId) || taken(tile) || IsClaimed(tile))
                {
                    return $"something is already at ({tile.Position.X}, {tile.Position.Y}), so it doesn't fit there";
                }
            }

            var walls = new HashSet<Location>(plan.Cells.Where(c => c.Level == 0).Select(c => BuildingRules.TileOf(corner, c)));
            var furnitureTiles = plan.Furniture.Select(f => BuildingRules.TileOf(corner, f.X * BuildingRules.CellsPerTile, f.Y * BuildingRules.CellsPerTile)).ToList();
            foreach (var tile in furnitureTiles.Where((t, i) => WorldObject.BlocksPassingType(plan.Furniture[i].Type)))
            {
                walls.Add(tile);
            }

            var planned = new HashSet<Location>(projects.Values.SelectMany(p => p.Plan.Cells.Where(c => c.Level == 0).Select(c => BuildingRules.TileOf(p.Corner, c))));
            bool Open(Location l, bool withNew) => map.IsWalkable(l.Position) && !world.IsBlocked(l) && !planned.Contains(l) && !(withNew && walls.Contains(l));

            var before = Reach(map, from, l => Open(l, false));
            var start = before.FirstOrDefault(l => !footprint.Contains(l));
            if (before.Count == 0 || start.MapId == null)
            {
                return "there's no way to it";
            }

            var after = Reach(map, start, l => Open(l, true));
            foreach (var l in before)
            {
                if (!walls.Contains(l) && !after.Contains(l))
                {
                    return $"it would shut in the ground at ({l.Position.X}, {l.Position.Y})";
                }
            }

            bool CanGetTo(Location l, HashSet<Location> reached) => reached.Contains(l) || Neighbours(l).Any(reached.Contains);
            foreach (var o in world.Objects.Where(o => o.Location.MapId == map.Id && o.Id != siteId && !footprint.Contains(o.Location)))
            {
                if (CanGetTo(o.Location, before) && !CanGetTo(o.Location, after))
                {
                    return $"nobody could get to the {o.Name.ToLowerInvariant()} any more";
                }
            }

            for (var i = 0; i < furnitureTiles.Count; i++)
            {
                if (!CanGetTo(furnitureTiles[i], after))
                {
                    return $"nobody could get to the {plan.Furniture[i].Name}";
                }
            }

            if (plan.DoorTile is (int dx, int dy))
            {
                standAt = BuildingRules.TileOf(corner, dx * BuildingRules.CellsPerTile, dy * BuildingRules.CellsPerTile);
                return null;
            }

            // No doorway: the builder stands on open ground beside it.
            foreach (var tile in footprint.SelectMany(Neighbours).Where(l => !footprint.Contains(l)).OrderBy(l => l.Position.Y).ThenBy(l => l.Position.X))
            {
                if (after.Contains(tile) && !world.Objects.Any(o => o.Location == tile))
                {
                    standAt = tile;
                    return null;
                }
            }

            return "there's nowhere beside it to stand and build";
        }

        private static IEnumerable<Location> Neighbours(Location l)
        {
            yield return new Location(l.MapId, l.Position.X, l.Position.Y - 1);
            yield return new Location(l.MapId, l.Position.X + 1, l.Position.Y);
            yield return new Location(l.MapId, l.Position.X, l.Position.Y + 1);
            yield return new Location(l.MapId, l.Position.X - 1, l.Position.Y);
        }

        /// <summary>Every open tile reachable from <paramref name="from"/> (which may itself be shut, like a tile someone stands on).</summary>
        private static HashSet<Location> Reach(TileMap map, Location from, Func<Location, bool> open)
        {
            var reached = new HashSet<Location>();
            var queue = new Queue<Location>();
            if (open(from))
            {
                reached.Add(from);
            }

            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                foreach (var n in Neighbours(queue.Dequeue()))
                {
                    if (n.Position.X >= 0 && n.Position.Y >= 0 && n.Position.X < map.Width && n.Position.Y < map.Height && !reached.Contains(n) && open(n))
                    {
                        reached.Add(n);
                        queue.Enqueue(n);
                    }
                }
            }

            return reached;
        }

        /// <summary>Lays the project's next blocks, up to <paramref name="max"/>, while the builder has the materials and nobody stands in the way.</summary>
        internal List<BlockCell> LayBlocks(Project project, string builderId, Belongings belongings, int max, Func<Location, bool> occupied, out string stoppedBy)
        {
            var laid = new List<BlockCell>();
            stoppedBy = null;
            while (laid.Count < max && project.Next is BlockCell next)
            {
                var tile = BuildingRules.TileOf(project.Corner, next);
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
                project.Builders.Add(builderId);
                Rebuild();
            }

            return laid;
        }

        /// <summary>The id of a finished building's furniture, numbered in plan order.</summary>
        public static string FurnitureIdOf(string siteId, BuildPlan plan, int index) => $"{siteId}{BuildingRules.BuiltInfix}{index + 1}";

        /// <summary>What a finished building's furniture is called: "Stove in the cabin", or the plan's own name for furniture standing alone.</summary>
        public static string FurnitureNameOf(BuildPlan plan, PlanFurniture f)
        {
            var name = plan.Cells.Count > 0 ? $"{f.Name} in the {plan.Name}" : plan.Furniture.Count == 1 ? plan.Name : $"{f.Name} at the {plan.Name}";
            return char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        private void Rebuild()
        {
            blocked.Clear();
            var placed = new List<WorldObject>();
            foreach (var site in sites.Values)
            {
                var p = At(site.Id);
                placed.Add(new WorldObject(site.Id, p == null ? "Building site" : $"{char.ToUpperInvariant(p.Plan.Name[0])}{p.Plan.Name.Substring(1)}", ObjectType.BuildPlot, site.StandAt));
                if (p == null)
                {
                    continue;
                }

                foreach (var cell in p.PlacedCells.Where(c => c.Level == 0))
                {
                    blocked.Add(BuildingRules.TileOf(p.Corner, cell));
                }

                if (p.IsFinished)
                {
                    for (var i = 0; i < p.Plan.Furniture.Count; i++)
                    {
                        var f = p.Plan.Furniture[i];
                        var tile = BuildingRules.TileOf(p.Corner, f.X * BuildingRules.CellsPerTile, f.Y * BuildingRules.CellsPerTile);
                        placed.Add(new WorldObject(FurnitureIdOf(p.SiteId, p.Plan, i), FurnitureNameOf(p.Plan, f), f.Type, tile));
                    }
                }
            }

            world.SetPlaced(PlacedSource, placed);
            Version++;
        }
    }
}
