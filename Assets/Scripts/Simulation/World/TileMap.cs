using System;
using System.Collections.Generic;

namespace TinyTown.Simulation.World
{
    public enum TileType
    {
        Grass,
        Path,
        Floor,
        Garden,
        Door,
        Wall,
        Tree,
        Water,
        House,
    }

    public sealed class TileMap
    {
        private readonly TileType[] tiles;

        public TileMap(string id, string name, bool isExterior, int width, int height, TileType[] tiles)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Map id is required.", nameof(id));
            }

            if (width <= 0 || height <= 0 || tiles == null || tiles.Length != width * height)
            {
                throw new ArgumentException($"Map '{id}' has inconsistent dimensions.");
            }

            Id = id;
            Name = name ?? id;
            IsExterior = isExterior;
            Width = width;
            Height = height;
            this.tiles = (TileType[])tiles.Clone();
        }

        public string Id { get; }

        public string Name { get; }

        public bool IsExterior { get; }

        public int Width { get; }

        public int Height { get; }

        public static bool IsWalkableType(TileType type)
        {
            return type == TileType.Grass || type == TileType.Path || type == TileType.Floor
                || type == TileType.Garden || type == TileType.Door;
        }

        public bool InBounds(GridPos p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;

        public TileType GetTile(GridPos p)
        {
            if (!InBounds(p))
            {
                throw new ArgumentOutOfRangeException(nameof(p), $"{p} is outside map '{Id}'.");
            }

            return tiles[(p.Y * Width) + p.X];
        }

        public bool IsWalkable(GridPos p) => InBounds(p) && IsWalkableType(GetTile(p));

        /// <summary>
        /// Builds a map from text rows (row 0 is the top). Every row must be the same length and every
        /// character must appear in <paramref name="legend"/>.
        /// </summary>
        public static TileMap FromRows(string id, string name, bool isExterior, IReadOnlyList<string> rows, IReadOnlyDictionary<char, TileType> legend)
        {
            if (rows == null || rows.Count == 0)
            {
                throw new ArgumentException($"Map '{id}' has no rows.");
            }

            var width = rows[0].Length;
            var result = new TileType[width * rows.Count];
            for (var y = 0; y < rows.Count; y++)
            {
                if (rows[y].Length != width)
                {
                    throw new ArgumentException($"Map '{id}' row {y} has length {rows[y].Length}, expected {width}.");
                }

                for (var x = 0; x < width; x++)
                {
                    if (!legend.TryGetValue(rows[y][x], out var type))
                    {
                        throw new ArgumentException($"Map '{id}' has unknown tile '{rows[y][x]}' at ({x}, {y}).");
                    }

                    result[(y * width) + x] = type;
                }
            }

            return new TileMap(id, name, isExterior, width, rows.Count, result);
        }
    }
}
