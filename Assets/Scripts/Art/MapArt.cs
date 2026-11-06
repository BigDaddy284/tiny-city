using System;
using System.Collections.Generic;
using TinyCity.Simulation.World;

namespace TinyCity.Art
{
    public enum ArtLayer
    {
        /// <summary>Things that stand up: trees, walls, buildings, furniture.</summary>
        Solid = 1,

        /// <summary>Lit windows, shown at night above the building they belong to.</summary>
        Glow = 2,
    }

    /// <summary>
    /// One picture to put in the world. The canvas's anchor sits on the centre of tile (<see cref="TileX"/>,
    /// <see cref="TileY"/>), and it is drawn in front of everything behind tile (<see cref="SortX"/>, <see cref="SortY"/>).
    /// </summary>
    public sealed class ArtPiece
    {
        public ArtPiece(string name, PixelCanvas canvas, int tileX, int tileY, int sortX, int sortY, ArtLayer layer)
        {
            Name = name;
            Canvas = canvas;
            TileX = tileX;
            TileY = tileY;
            SortX = sortX;
            SortY = sortY;
            Layer = layer;
        }

        public string Name { get; }

        public PixelCanvas Canvas { get; }

        public int TileX { get; }

        public int TileY { get; }

        public int SortX { get; }

        public int SortY { get; }

        public ArtLayer Layer { get; }
    }

    /// <summary>
    /// A request for a replacement picture, e.g. a PNG the player dropped into the art folder.
    /// <see cref="Key"/> names the picture (such as <c>tree-winter</c> or <c>object-bench</c>); the footprint says how many
    /// tiles it covers, which fixes where it stands (see <see cref="MapArt.AnchorFor"/>).
    /// </summary>
    public readonly struct ArtRequest
    {
        public ArtRequest(string key, int footprintWidth, int footprintDepth)
        {
            Key = key;
            FootprintWidth = footprintWidth;
            FootprintDepth = footprintDepth;
        }

        public string Key { get; }

        public int FootprintWidth { get; }

        public int FootprintDepth { get; }
    }

    /// <summary>Everything drawn for one map: its ground, and the pieces that stand on it.</summary>
    public sealed class MapArt
    {
        private MapArt(PixelCanvas ground, List<ArtPiece> pieces)
        {
            Ground = ground;
            Pieces = pieces;
        }

        /// <summary>The flat ground, anchored on the centre of tile (0, 0).</summary>
        public PixelCanvas Ground { get; }

        public IReadOnlyList<ArtPiece> Pieces { get; }

        /// <summary>
        /// Draws a map. <paramref name="replacement"/> may supply a picture to use instead of a generated one (return
        /// null to keep the generated one).
        /// </summary>
        public static MapArt Build(TownWorld world, TileMap map, Palette palette, Func<ArtRequest, PixelCanvas> replacement = null)
        {
            var season = palette.Season.ToString().ToLowerInvariant();
            PixelCanvas Pick(string key, int w, int d, Func<PixelCanvas> make)
            {
                return replacement?.Invoke(new ArtRequest($"{key}-{season}", w, d)) ?? replacement?.Invoke(new ArtRequest(key, w, d)) ?? make();
            }

            var ground = Pick($"ground-{map.Id}", map.Width, map.Height, () => GroundArt.Render(map, palette));
            var pieces = new List<ArtPiece>();

            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    var type = map.GetTile(new GridPos(x, y));
                    if (type == TileType.Tree)
                    {
                        var variant = (int)(Noise.Hash(x, y, Noise.Of(map.Id)) & 0x7fffffff);
                        var key = NatureArt.IsPine(variant) ? "pine" : "tree";
                        pieces.Add(new ArtPiece($"Tree {x},{y}", Pick(key, 1, 1, () => NatureArt.Tree(palette, variant)), x, y, x, y, ArtLayer.Solid));
                    }
                }
            }

            foreach (var obj in world.Objects)
            {
                if (obj.Location.MapId != map.Id || world.IsPlaced(obj))
                {
                    continue;
                }

                var p = obj.Location.Position;
                var variant = Noise.Of(obj.Id);
                var key = $"object-{obj.Type.ToString().ToLowerInvariant()}";
                pieces.Add(new ArtPiece(obj.Name, Pick(key, 1, 1, () => FurnitureArt.Render(obj.Type, variant, palette, map.IsExterior)), p.X, p.Y, p.X, p.Y, ArtLayer.Solid));
            }

            return new MapArt(ground, pieces);
        }

        /// <summary>
        /// Where a replacement picture stands: horizontally centred on its footprint, with its bottom edge on the
        /// footprint's nearest corner. Returned as the anchor pixel (the centre of the footprint's corner tile).
        /// </summary>
        public static (double x, double y) AnchorFor(int imageWidth, int imageHeight, int footprintWidth, int footprintDepth)
        {
            return ((imageWidth / 2.0) - ((footprintWidth - footprintDepth) * IsoPainter.HalfTileHeight), imageHeight - ((footprintWidth + footprintDepth - 1) * IsoPainter.HalfTileHeight));
        }
    }
}
