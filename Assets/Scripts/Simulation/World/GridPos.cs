using System;

namespace TinyTown.Simulation.World
{
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }

        public int Y { get; }

        public bool Equals(GridPos other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is GridPos other && Equals(other);

        public override int GetHashCode() => (X * 397) ^ Y;

        public static bool operator ==(GridPos a, GridPos b) => a.Equals(b);

        public static bool operator !=(GridPos a, GridPos b) => !a.Equals(b);

        public override string ToString() => $"({X}, {Y})";
    }

    /// <summary>A tile on a specific map. This is a resident's logical position and what save files store.</summary>
    public readonly struct Location : IEquatable<Location>
    {
        public Location(string mapId, GridPos position)
        {
            MapId = mapId ?? throw new ArgumentNullException(nameof(mapId));
            Position = position;
        }

        public Location(string mapId, int x, int y)
            : this(mapId, new GridPos(x, y))
        {
        }

        public string MapId { get; }

        public GridPos Position { get; }

        public bool Equals(Location other) => MapId == other.MapId && Position == other.Position;

        public override bool Equals(object obj) => obj is Location other && Equals(other);

        public override int GetHashCode() => ((MapId?.GetHashCode() ?? 0) * 397) ^ Position.GetHashCode();

        public static bool operator ==(Location a, Location b) => a.Equals(b);

        public static bool operator !=(Location a, Location b) => !a.Equals(b);

        public override string ToString() => $"{MapId} {Position}";
    }
}
