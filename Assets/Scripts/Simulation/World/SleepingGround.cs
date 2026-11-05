using System;
using System.Collections.Generic;
using System.Linq;

namespace TinyCity.Simulation.World
{
    /// <summary>A patch of bare ground someone has lain down on to sleep (Tiny City 2). It lasts only while they sleep there.</summary>
    public sealed class SleepingSpot
    {
        public const string IdPrefix = "obj-sleep-";

        public SleepingSpot(string id, Location location, string sleeperId)
        {
            Id = id;
            Location = location;
            SleeperId = sleeperId;
        }

        public string Id { get; }

        public Location Location { get; }

        public string SleeperId { get; }
    }

    /// <summary>
    /// Tiny City 2: the spots on open ground where people are sleeping, put in the world as <see cref="ObjectType.SleepingSpot"/>s
    /// so the sleeper has somewhere to walk to and lie on. Each is gone once its sleeper gets up. Saved with the town.
    /// </summary>
    public sealed class SleepingGround
    {
        public const string PlacedSource = "sleeping";

        private readonly SortedDictionary<string, SleepingSpot> spots = new SortedDictionary<string, SleepingSpot>(StringComparer.Ordinal);
        private TownWorld world;

        /// <summary>The number the next spot's id gets. Saved, so ids are never reused.</summary>
        public int NextSpotNumber { get; internal set; } = 1;

        public IEnumerable<SleepingSpot> Spots => spots.Values;

        /// <summary>Puts the spots in this world (and none from an earlier town). Called by the simulation.</summary>
        internal void Attach(TownWorld townWorld)
        {
            world = townWorld;
            spots.Clear();
            Rebuild();
        }

        internal SleepingSpot Add(Location location, string sleeperId)
        {
            var spot = new SleepingSpot($"{SleepingSpot.IdPrefix}{NextSpotNumber++}", location, sleeperId);
            spots[spot.Id] = spot;
            Rebuild();
            return spot;
        }

        internal void Restore(SleepingSpot spot)
        {
            spots[spot.Id] = spot;
            Rebuild();
        }

        /// <summary>Lets go of every spot whose sleeper is no longer heading for it or lying on it.</summary>
        internal void Prune(Func<SleepingSpot, bool> inUse)
        {
            var gone = spots.Values.Where(s => !inUse(s)).Select(s => s.Id).ToList();
            foreach (var id in gone)
            {
                spots.Remove(id);
            }

            if (gone.Count > 0)
            {
                Rebuild();
            }
        }

        private void Rebuild() => world?.SetPlaced(PlacedSource, spots.Values.Select(s => new WorldObject(s.Id, "Sleeping spot", ObjectType.SleepingSpot, s.Location)));
    }
}
