using System.Collections.Generic;

namespace TinyTown.Simulation.World
{
    /// <summary>
    /// Deterministic tile navigation across maps. Residents ask for one step at a time, so no path needs to be
    /// stored or saved: the same position and destination always give the same next step.
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

        public Pathfinder(TownWorld world)
        {
            this.world = world;
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

            // Dijkstra over doors. Maps are few and small, so a simple O(n^2) search is enough.
            var portals = world.Portals;
            var arrival = new int[portals.Count];
            var firstHop = new Portal[portals.Count];
            var done = new bool[portals.Count];
            for (var i = 0; i < portals.Count; i++)
            {
                arrival[i] = Unreachable;
                if (portals[i].From.MapId == from.MapId)
                {
                    var d = SameMapDistance(from, portals[i].From);
                    if (d != Unreachable)
                    {
                        arrival[i] = d + 1;
                        firstHop[i] = portals[i];
                    }
                }
            }

            while (true)
            {
                var current = -1;
                for (var i = 0; i < portals.Count; i++)
                {
                    if (!done[i] && arrival[i] != Unreachable && (current < 0 || arrival[i] < arrival[current]))
                    {
                        current = i;
                    }
                }

                if (current < 0)
                {
                    break;
                }

                done[current] = true;
                var landing = portals[current].To;
                if (landing.MapId == to.MapId)
                {
                    var d = SameMapDistance(landing, to);
                    if (d != Unreachable && (best == Unreachable || arrival[current] + d < best))
                    {
                        best = arrival[current] + d;
                        firstPortal = firstHop[current];
                    }
                }

                for (var i = 0; i < portals.Count; i++)
                {
                    if (done[i] || portals[i].From.MapId != landing.MapId)
                    {
                        continue;
                    }

                    var d = SameMapDistance(landing, portals[i].From);
                    if (d == Unreachable)
                    {
                        continue;
                    }

                    var candidate = arrival[current] + d + 1;
                    if (arrival[i] == Unreachable || candidate < arrival[i])
                    {
                        arrival[i] = candidate;
                        firstHop[i] = firstHop[current];
                    }
                }
            }

            return best;
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
                        queue.Enqueue(n);
                    }
                }
            }

            distanceFields[target] = field;
            return field;
        }

        private static int Index(TileMap map, GridPos p) => (p.Y * map.Width) + p.X;
    }
}
