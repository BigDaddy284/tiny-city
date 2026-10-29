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
    /// <see cref="Key"/> names the picture (such as <c>tree-winter</c> or <c>object-bed</c>); the footprint says how many
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
        /// null to keep the generated one); building replacements are cut into strips like the generated buildings.
        /// </summary>
        public static MapArt Build(TownWorld world, TileMap map, Palette palette, Func<ArtRequest, PixelCanvas> replacement = null)
        {
            var kind = PlaceStyle.Of(world, map);
            var season = palette.Season.ToString().ToLowerInvariant();
            PixelCanvas Pick(string key, int w, int d, Func<PixelCanvas> make)
            {
                return replacement?.Invoke(new ArtRequest($"{key}-{season}", w, d)) ?? replacement?.Invoke(new ArtRequest(key, w, d)) ?? make();
            }

            var ground = Pick($"ground-{map.Id}", map.Width, map.Height, () => GroundArt.Render(map, kind, palette));
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
                    else if (!map.IsExterior && (type == TileType.Wall || type == TileType.Door))
                    {
                        var wall = InteriorArt.Render(map, kind, x, y);
                        if (wall != null)
                        {
                            pieces.Add(new ArtPiece($"Wall {x},{y}", wall, x, y, x, y, ArtLayer.Solid));
                        }
                    }
                }
            }

            if (map.IsExterior)
            {
                foreach (var plan in BuildingArt.Find(world, map))
                {
                    var key = $"building-{plan.InteriorMapId ?? "house"}";
                    var custom = replacement?.Invoke(new ArtRequest($"{key}-{season}", plan.Width, plan.Depth)) ?? replacement?.Invoke(new ArtRequest(key, plan.Width, plan.Depth));
                    var (body, glow) = custom != null ? (custom, null) : BuildingArt.Render(plan, palette);
                    foreach (var (strip, sx, sy) in BuildingArt.Strips(body, plan))
                    {
                        pieces.Add(new ArtPiece($"Building {plan.InteriorMapId} {sx},{sy}", strip, plan.X, plan.Y, sx, sy, ArtLayer.Solid));
                    }

                    if (glow != null)
                    {
                        foreach (var (strip, sx, sy) in BuildingArt.Strips(glow, plan))
                        {
                            pieces.Add(new ArtPiece($"Windows {plan.InteriorMapId} {sx},{sy}", strip, plan.X, plan.Y, sx, sy, ArtLayer.Glow));
                        }
                    }
                }
            }

            foreach (var obj in world.Objects)
            {
                if (obj.Location.MapId != map.Id)
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
