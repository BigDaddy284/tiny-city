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
    /// <see cref="PopulationRules.MaxNewcomers"/> ever and never more than <see cref="PopulationRules.MaxLiving"/>
    /// alive. They move into a home nobody lives in, or, when every home is taken, sleep on a park bench until one
    /// comes free.
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
        public bool HasRoomForNewcomer => NewcomerCount < PopulationRules.MaxNewcomers && LivingCount < PopulationRules.MaxLiving;

        private void StepNewcomers()
        {
            if (Time.MinuteOfDay == PopulationRules.HousingMinute)
            {
                HouseTheHomeless();
                FillVacancies();
            }

            if (!NewcomersEnabled || Time.MinuteOfDay != PopulationRules.ArrivalMinute || !PopulationRules.SomeoneArrives(World.TownId, Time.Day))
            {
                return;
            }

            MoveInNewcomer();
        }

        /// <summary>Each morning anyone sleeping rough moves into a home that has come free, oldest resident id first.</summary>
        internal void HouseTheHomeless()
        {
            foreach (var r in residents.Where(r => r.IsAlive && r.IsHomeless).ToList())
            {
                var home = PickFreeHome(r.Id);
                if (home == null)
                {
                    return;
                }

                MoveInto(r, home);
                r.AddLog(Time, $"Moved into {home.Name}");
                Memories.Add(r.Id, Time, MemoryType.Event, 7, $"After sleeping rough, I finally have a home of my own: {home.Name}.", tags: new[] { home.Id, "home", "housed" });
                History.Add(Time, TownEventKind.Housed, $"{r.FullName} found a home at {home.Name}.", new[] { r.Id });
            }
        }

        /// <summary>
        /// Leaves the resident with nowhere to live: no home, no bed and no rent. They sleep on a park bench until a
        /// home comes free.
        /// </summary>
        internal void MakeHomeless(Resident r, string reason)
        {
            r.HomeMapId = null;
            r.BedObjectId = null;
            r.WeeklyRentPence = 0;
            r.Groceries = 0;
            r.AddLog(Time, "Has nowhere to live, so sleeps on a park bench");
            Memories.Add(r.Id, Time, MemoryType.Event, 7, $"I have nowhere to live, so I sleep on a park bench. {reason}", tags: new[] { "home", "homeless" });
            History.Add(Time, TownEventKind.Homeless, $"{r.FullName} has nowhere to live and sleeps on a park bench.", new[] { r.Id });
        }

        private void MoveInto(Resident r, TileMap home)
        {
            r.HomeMapId = home.Id;
            r.BedObjectId = BedIn(home).Id;
            r.WeeklyRentPence = Newcomers.RentFor(home);
        }

        /// <summary>A free home picked by <see cref="Chance"/>, or null when every home is taken.</summary>
        private TileMap PickFreeHome(string forId)
        {
            var homes = FreeHomes().ToList();
            return homes.Count == 0 ? null : homes[(int)(Chance.Roll("home:" + forId, Time.Day, 0) * homes.Count)];
        }

        private WorldObject BedIn(TileMap home) =>
            World.Objects.Where(o => o.Location.MapId == home.Id && o.Type == ObjectType.Bed).OrderBy(o => o.Id, System.StringComparer.Ordinal).First();

        /// <summary>Someone moves in now, if there's room. Returns them, or null. Called by the daily roll, and by tests.</summary>
        internal Resident MoveInNewcomer()
        {
            if (!HasRoomForNewcomer)
            {
                return null;
            }

            var id = NextResidentId();
            var home = PickFreeHome(id);
            var arrival = ArrivalPoint();
            if (arrival == null)
            {
                return null;
            }

            // Everyone who has ever lived here, the dead too: their family name is still theirs.
            var surnames = new HashSet<string>(residents.Select(r => r.LastName));
            var newcomer = Newcomers.Create(id, Time.Day, arrival.Value, surnames);
            AddResident(newcomer);
            if (home != null)
            {
                MoveInto(newcomer, home);
                newcomer.AddLog(Time, $"Moved to the city, into {home.Name}");
                newcomer.Goal = $"Just arrived; heading home to {home.Name}";
                Memories.Add(newcomer.Id, Time, MemoryType.LifeHistory, 7, $"I moved to {World.TownName} from out of town and into {home.Name}.", tags: new[] { home.Id, "moved-in", "home" });
                History.Add(Time, TownEventKind.MovedIn, $"{newcomer.FullName} moved to the city, into {home.Name}.", new[] { newcomer.Id });
            }
            else
            {
                newcomer.Groceries = 0;
                newcomer.AddLog(Time, "Moved to the city, but every home was taken");
                Memories.Add(newcomer.Id, Time, MemoryType.LifeHistory, 7, $"I moved to {World.TownName} from out of town, but every home was taken, so I sleep on a park bench.", tags: new[] { "moved-in", "home", "homeless" });
                History.Add(Time, TownEventKind.MovedIn, $"{newcomer.FullName} moved to the city, but every home was taken, so they sleep on a park bench.", new[] { newcomer.Id });
            }

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
        /// Where a newcomer first appears: on the path closest to the edge of the town's biggest outdoor map, as if they
        /// had just walked in (the lowest, then leftmost, of those).
        /// </summary>
        private Location? ArrivalPoint()
        {
            var outside = World.Maps.Where(m => m.IsExterior).OrderByDescending(m => m.Width * m.Height).ThenBy(m => m.Id, System.StringComparer.Ordinal).FirstOrDefault();
            if (outside == null)
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

            return best;
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
