using UnityEngine;

namespace TinyTown.Runtime
{
    /// <summary>Converts map tiles to isometric world positions and draw order. One tile is 1 unit wide and 0.5 tall.</summary>
    public static class IsoGrid
    {
        public const float TileWidth = 1f;
        public const float TileHeight = 0.5f;

        public static Vector3 TileToWorld(float x, float y)
        {
            return new Vector3((x - y) * TileWidth * 0.5f, -(x + y) * TileHeight * 0.5f, 0f);
        }

        /// <summary>Tiles further down the screen (larger x + y) draw in front.</summary>
        public static int SortingOrder(int x, int y, int layer) => 1000 + ((x + y) * 10) + layer;
    }
}
