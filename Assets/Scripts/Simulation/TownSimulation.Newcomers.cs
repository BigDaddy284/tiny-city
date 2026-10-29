using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Health;
using TinyCity.Simulation.History;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.World;

namespace TinyCity.Simulation
{
    /// <summary>
    /// People moving to the city from out of town, following <see cref="PopulationRules"/>: at most
    /// <see cref="PopulationRules.MaxNewcomers"/> ever, never more than <see cref="PopulationRules.MaxLiving"/>
    /// alive, and only into a home nobody lives in.
    /// </summary>
    public sealed partial class TownSimulation
    {
        /// <summary>
        /// Whether newcomers move in. Off unless turned on, so small test towns keep the residents they were given; the
        /// game turns it on from settings.json <c>simulation.newcomers</c>.
        /// </summary>
        public bool NewcomersEnabled { get; set; }

        /// <summary>Everyone who has ever moved in from out of town, alive or not.</summary>
        public int NewcomerCount => residents.Count(r => r.IsNewcomer);

        public int LivingCount => residents.Count(r => r.IsAlive);

        /// <summary>Whether there's room for one more newcomer right now (ignoring the day's luck).</summary>
        public bool HasRoomForNewcomer => NewcomerCount < PopulationRules.MaxNewcomers && LivingCount < PopulationRules.MaxLiving && FreeHomes().Any();

        private void StepNewcomers()
        {
            if (!NewcomersEnabled || Time.MinuteOfDay != PopulationRules.ArrivalMinute || !PopulationRules.SomeoneArrives(World.TownId, Time.Day))
            {
                return;
            }

            MoveInNewcomer();
        }

        /// <summary>Someone moves in now, if there's room. Returns them, or null. Called by the daily roll, and by tests.</summary>
        internal Resident MoveInNewcomer()
        {
            if (!HasRoomForNewcomer)
            {
                return null;
            }

            var homes = FreeHomes().ToList();
            var home = homes[(int)(Chance.Roll("arrival-home:" + World.TownId, Time.Day, NewcomerCount) * homes.Count)];
            var bed = World.Objects.Where(o => o.Location.MapId == home.Id && o.Type == ObjectType.Bed).OrderBy(o => o.Id, System.StringComparer.Ordinal).First();
            var arrival = ArrivalPoint(home);
            if (arrival == null)
            {
                return null;
            }

            var taken = new HashSet<string>(residents.Select(r => r.FullName));
            var newcomer = Newcomers.Create(NextResidentId(), Time.Day, home, bed, arrival.Value, taken);
            AddResident(newcomer);
            newcomer.AddLog(Time, $"Moved to the city, into {home.Name}");
            newcomer.Goal = $"Just arrived; heading home to {home.Name}";
            Memories.Add(newcomer.Id, Time, MemoryType.LifeHistory, 7, $"I moved to {World.TownName} from out of town and into {home.Name}.", tags: new[] { home.Id, "moved-in", "home" });
            History.Add(Time, TownEventKind.MovedIn, $"{newcomer.FullName} moved to the city, into {home.Name}.", new[] { newcomer.Id });
            return newcomer;
        }

        /// <summary>Homes nobody alive lives in: indoor maps with a bed that aren't a business, in map order.</summary>
        public IEnumerable<TileMap> FreeHomes()
        {
            var lived = new HashSet<string>(residents.Where(r => r.IsAlive).Select(r => r.HomeMapId));
            return World.Maps.Where(m => !m.IsExterior && World.BusinessOnMap(m.Id) == null && !lived.Contains(m.Id)
                && World.Objects.Any(o => o.Location.MapId == m.Id && o.Type == ObjectType.Bed));
        }

        /// <summary>
        /// Where a newcomer first appears: on the path closest to the edge of the outdoor map their home opens onto, as
        /// if they had just walked in (the lowest, then leftmost, of those).
        /// </summary>
        private Location? ArrivalPoint(TileMap home)
        {
            var door = World.PortalsFrom(home.Id).Select(p => p.To).FirstOrDefault(l => World.TryGetMap(l.MapId, out var m) && m.IsExterior);
            if (door.MapId == null || !World.TryGetMap(door.MapId, out var outside))
            {
                return null;
            }

            Location? best = null;
            var bestEdge = int.MaxValue;
            for (var y = outside.Height - 1; y >= 0; y--)
            {
                for (var x = 0; x < outside.Width; x++)
                {
                    var at = new Location(outside.Id, new GridPos(x, y));
                    if (outside.GetTile(at.Position) != TileType.Path || World.IsBlocked(at))
                    {
                        continue;
                    }

                    var edge = System.Math.Min(System.Math.Min(x, y), System.Math.Min(outside.Width - 1 - x, outside.Height - 1 - y));
                    if (edge < bestEdge)
                    {
                        best = at;
                        bestEdge = edge;
                    }
                }
            }

            return best ?? door;
        }

        /// <summary>The next unused resident id after the highest "res-NNNN" in town.</summary>
        private string NextResidentId()
        {
            var highest = 0;
            foreach (var r in residents)
            {
                if (r.Id.StartsWith("res-") && int.TryParse(r.Id.Substring(4), out var n) && n > highest)
                {
                    highest = n;
                }
            }

            return $"res-{highest + 1:0000}";
        }
    }
}
