using System.Collections.Generic;

namespace TinyCity.Simulation.World
{
    /// <summary>
    /// Deterministic tile navigation across maps. Residents ask for one step at a time, so no path needs to be
    /// stored or saved: the same position and destination always give the same next step. Residents walk around
    /// furniture (<see cref="TownWorld.IsBlocked"/>) and step onto it only when it is where they are going.
    /// </summary>
    public sealed class Pathfinder
    {
        public const int Unreachable = -1;

        private static readonly GridPos[] Directions =
        {
            new GridPos(0, -1),
            new GridPos(1, 0),
            new GridPos(0, 1),
            new GridPos(-1, 0),
        };

        private readonly TownWorld world;
        private readonly Dictionary<Location, int[]> distanceFields = new Dictionary<Location, int[]>();
        private readonly Dictionary<Location, int[]> doorCosts = new Dictionary<Location, int[]>();
        private readonly Dictionary<string, List<int>> portalsFrom = new Dictionary<string, List<int>>();
        private readonly Dictionary<string, List<int>> portalsInto = new Dictionary<string, List<int>>();

        public Pathfinder(TownWorld world)
        {
            this.world = world;
            for (var i = 0; i < world.Portals.Count; i++)
            {
                Add(portalsFrom, world.Portals[i].From.MapId, i);
                Add(portalsInto, world.Portals[i].To.MapId, i);
            }
        }

        private static void Add(Dictionary<string, List<int>> byMap, string mapId, int portal)
        {
            if (!byMap.TryGetValue(mapId, out var list))
            {
                byMap[mapId] = list = new List<int>();
            }

            list.Add(portal);
        }

        /// <summary>Total walking steps from one location to another, including door transitions, or <see cref="Unreachable"/>.</summary>
        public int Distance(Location from, Location to) => Plan(from, to, out _);

        /// <summary>
        /// The next location to move to from <paramref name="from"/> toward <paramref name="to"/>. Returns false if
        /// the destination is unreachable or already reached. The next location is on another map when the
        /// resident is standing on a door that leads the right way.
        /// </summary>
        public bool TryGetNextStep(Location from, Location to, out Location next)
        {
            next = from;
            if (from == to)
            {
                return false;
            }

            var cost = Plan(from, to, out var firstPortal);
            if (cost == Unreachable)
            {
                return false;
            }

            if (firstPortal != null)
            {
                if (from == firstPortal.From)
                {
                    next = firstPortal.To;
                    return true;
                }

                return TryStepToward(from, firstPortal.From, out next);
            }

            return TryStepToward(from, to, out next);
        }

        private int Plan(Location from, Location to, out Portal firstPortal)
        {
            firstPortal = null;
            if (!world.IsWalkable(from) || !world.IsWalkable(to))
            {
                return Unreachable;
            }

            var best = Unreachable;
            if (from.MapId == to.MapId)
            {
                best = SameMapDistance(from, to);
            }

            // Through a door on this map: walk to it, then the cached cost from that door to the target.
            if (portalsFrom.TryGetValue(from.MapId, out var doors))
            {
                var viaDoor = DoorCosts(to);
                var portals = world.Portals;
                foreach (var i in doors)
                {
                    if (viaDoor[i] == Unreachable)
                    {
                        continue;
                    }

                    var d = SameMapDistance(from, portals[i].From);
                    if (d != Unreachable && (best == Unreachable || d + viaDoor[i] < best))
                    {
                        best = d + viaDoor[i];
                        firstPortal = portals[i];
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// For each door, the steps from standing on it to <paramref name="target"/>, going through it (one step) and
        /// on through any further doors, or <see cref="Unreachable"/>. Dijkstra backwards from the target over the
        /// doors, cached per target like the distance fields, so a route costs only the doors on the walker's map.
        /// </summary>
        private int[] DoorCosts(Location target)
        {
            if (doorCosts.TryGetValue(target, out var cached))
            {
                return cached;
            }

            var portals = world.Portals;
            var cost = new int[portals.Count];
            var done = new bool[portals.Count];
            for (var i = 0; i < portals.Count; i++)
            {
                cost[i] = Unreachable;
                if (portals[i].To.MapId == target.MapId)
                {
                    var d = SameMapDistance(portals[i].To, target);
                    if (d != Unreachable)
                    {
                        cost[i] = d + 1;
                    }
                }
            }

            while (true)
            {
                var current = -1;
                for (var i = 0; i < portals.Count; i++)
                {
                    if (!done[i] && cost[i] != Unreachable && (current < 0 || cost[i] < cost[current]))
                    {
                        current = i;
                    }
                }

                if (current < 0)
                {
                    break;
                }

                done[current] = true;

                // Doors that land on the map this one leaves from can reach the target through it.
                if (!portalsInto.TryGetValue(portals[current].From.MapId, out var feeding))
                {
                    continue;
                }

                foreach (var i in feeding)
                {
                    if (done[i])
                    {
                        continue;
                    }

                    var d = SameMapDistance(portals[i].To, portals[current].From);
                    if (d == Unreachable)
                    {
                        continue;
                    }

                    var candidate = cost[current] + d + 1;
                    if (cost[i] == Unreachable || candidate < cost[i])
                    {
                        cost[i] = candidate;
                    }
                }
            }

            doorCosts[target] = cost;
            return cost;
        }

        private int SameMapDistance(Location from, Location to)
        {
            var map = world.GetMap(to.MapId);
            return DistanceField(to)[Index(map, from.Position)];
        }

        private bool TryStepToward(Location from, Location target, out Location next)
        {
            var map = world.GetMap(target.MapId);
            var field = DistanceField(target);
            var here = field[Index(map, from.Position)];
            foreach (var dir in Directions)
            {
                var p = new GridPos(from.Position.X + dir.X, from.Position.Y + dir.Y);
                if (map.InBounds(p) && field[Index(map, p)] == here - 1 && here > 0)
                {
                    next = new Location(map.Id, p);
                    return true;
                }
            }

            next = from;
            return false;
        }

        /// <summary>Breadth-first distances from every tile on the target's map to the target. Cached; maps never change.</summary>
        private int[] DistanceField(Location target)
        {
            if (distanceFields.TryGetValue(target, out var cached))
            {
                return cached;
            }

            var map = world.GetMap(target.MapId);
            var field = new int[map.Width * map.Height];
            for (var i = 0; i < field.Length; i++)
            {
                field[i] = Unreachable;
            }

            var queue = new Queue<GridPos>();
            field[Index(map, target.Position)] = 0;
            queue.Enqueue(target.Position);
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                var d = field[Index(map, p)];
                foreach (var dir in Directions)
                {
                    var n = new GridPos(p.X + dir.X, p.Y + dir.Y);
                    if (map.IsWalkable(n) && field[Index(map, n)] == Unreachable)
                    {
                        field[Index(map, n)] = d + 1;

                        // Furniture gets a distance, so a resident using it can step off, but no path runs through it.
                        if (!world.IsBlocked(new Location(map.Id, n)))
                        {
                            queue.Enqueue(n);
                        }
                    }
                }
            }

            distanceFields[target] = field;
            return field;
        }

        private static int Index(TileMap map, GridPos p) => (p.Y * map.Width) + p.X;
    }
}
