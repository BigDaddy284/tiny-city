using System;

namespace TinyCity.Simulation.World
{
    public enum ObjectType
    {
        Bed,
        Kitchen,
        Sink,
        Toilet,
        Shower,
        Armchair,
        GardenPlot,
        GardenTap,
        Bench,
        ExerciseSpot,
        Workbench,

        /// <summary>Shelves in the general store, where customers pick up groceries.</summary>
        ShopShelf,

        /// <summary>A serving counter: where shop, café, bakery and clinic staff work.</summary>
        Counter,
        CafeTable,

        /// <summary>The bakery's display of cakes and pastries.</summary>
        BakeryDisplay,
        ClinicBed,
        Easel,
        Piano,
        ChessTable,
        FishingSpot,

        /// <summary>Where a community event is held. Holds a crowd.</summary>
        GatheringSpot,
    }

    /// <summary>Fixed furniture or outdoor feature. A resident uses it by standing on its tile.</summary>
    public sealed class WorldObject
    {
        public WorldObject(string id, string name, ObjectType type, Location location)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Object id is required.", nameof(id));
            }

            Id = id;
            Name = name ?? id;
            Type = type;
            Location = location;
        }

        public string Id { get; }

        public string Name { get; }

        public ObjectType Type { get; }

        public Location Location { get; }

        /// <summary>How many residents can use or head for it at once. Benches seat two; most things serve one.</summary>
        public int Capacity => CapacityOf(Type);

        /// <summary>
        /// Furniture and fittings stand in the way: residents walk around them and step onto one only to use it.
        /// Open ground (garden plots, spots for exercise, fishing or gathering) can be crossed.
        /// </summary>
        public bool BlocksPassing => BlocksPassingType(Type);

        public static bool BlocksPassingType(ObjectType type)
        {
            switch (type)
            {
                case ObjectType.GardenPlot:
                case ObjectType.ExerciseSpot:
                case ObjectType.FishingSpot:
                case ObjectType.GatheringSpot:
                    return false;
                default:
                    return true;
            }
        }

        public static int CapacityOf(ObjectType type)
        {
            switch (type)
            {
                case ObjectType.Bench:
                case ObjectType.ChessTable:
                case ObjectType.FishingSpot:
                case ObjectType.ExerciseSpot:
                case ObjectType.Workbench:
                    return 2;
                case ObjectType.GardenPlot:
                case ObjectType.CafeTable:
                    return 4;
                case ObjectType.ShopShelf:
                case ObjectType.BakeryDisplay:
                    return 3;
                case ObjectType.Counter:
                    return 2;
                case ObjectType.GatheringSpot:
                    return 12;
                default:
                    return 1;
            }
        }
    }

    /// <summary>One-way link between two walkable tiles on different maps, such as a front door.</summary>
    public sealed class Portal
    {
        public Portal(string id, Location from, Location to)
        {
            if (from.MapId == to.MapId)
            {
                throw new ArgumentException($"Portal '{id}' must connect two different maps.");
            }

            Id = id;
            From = from;
            To = to;
        }

        public string Id { get; }

        public Location From { get; }

        public Location To { get; }
    }
}
