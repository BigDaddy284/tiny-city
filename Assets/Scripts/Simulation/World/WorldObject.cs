using System;

namespace TinyCity.Simulation.World
{
    public enum ObjectType
    {
        Kitchen,
        GardenPlot,
        Bench,
        Workbench,
        FishingSpot,

        /// <summary>Where the weekly town meeting is held. Holds a crowd.</summary>
        GatheringSpot,

        /// <summary>A market stall, where one resident hands out food to others, two at a time.</summary>
        MarketStall,

        /// <summary>A stand of trees where wood can be gathered, two at a time.</summary>
        Woodland,

        /// <summary>A pile of rocks where stone can be broken off with a pickaxe, two at a time.</summary>
        RockPile,

        /// <summary>A building site a resident chose, where they plan and build with blocks (where the builder stands).</summary>
        BuildPlot,

        /// <summary>Fallen branches under the trees, picked up by hand for a little wood, two at a time.</summary>
        StickPile,

        /// <summary>Loose stones by the rocks, picked up by hand for a little stone, two at a time.</summary>
        LooseStones,
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

        /// <summary>How many residents can use or head for it at once. Benches and café tables (one chair each side) seat two; most things serve one.</summary>
        /// <summary>
        /// Owner, 6 November: the merchant keeps the camp store at the camp stove nearly all day, so that stove has
        /// room for them, a cook and people bringing things or taking them out. Other stoves take one cook.
        /// </summary>
        public const int CampStoveCapacity = 4;

        public int Capacity => Id == Economy.CampStore.StoveId ? CampStoveCapacity : CapacityOf(Type);

        /// <summary>
        /// Furniture and fittings stand in the way: residents walk around them and step onto one only to use it.
        /// Open ground (garden plots and crop patches, spots for fishing, gathering or meeting, building sites) can be crossed.
        /// </summary>
        public bool BlocksPassing => BlocksPassingType(Type);

        public static bool BlocksPassingType(ObjectType type)
        {
            switch (type)
            {
                case ObjectType.GardenPlot:
                case ObjectType.FishingSpot:
                case ObjectType.GatheringSpot:
                case ObjectType.Woodland:
                case ObjectType.RockPile:
                case ObjectType.BuildPlot:
                case ObjectType.StickPile:
                case ObjectType.LooseStones:
                    return false;
                default:
                    return true;
            }
        }

        public static int CapacityOf(ObjectType type)
        {
            switch (type)
            {
                // Owner's log, 6 November: with only the pond to drink from, twenty people kept finding its three
                // spots full by the time their answer came, so each spot round the pond takes four.
                case ObjectType.FishingSpot:
                    return 4;
                case ObjectType.Bench:
                case ObjectType.Workbench:
                case ObjectType.MarketStall:
                case ObjectType.Woodland:
                case ObjectType.RockPile:
                case ObjectType.StickPile:
                case ObjectType.LooseStones:
                    return 2;
                case ObjectType.GardenPlot:
                case ObjectType.BuildPlot:
                    return 4;
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
