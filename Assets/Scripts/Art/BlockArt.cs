using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Building;
using TinyCity.Simulation.Economy;
using TinyCity.Simulation.World;

namespace TinyCity.Art
{
    /// <summary>Tiny City 2's building blocks: each column of laid blocks on a plot drawn as a stack of cubes.</summary>
    public static class BlockArt
    {
        /// <summary>How tall one block is drawn, in pixels.</summary>
        public const int BlockHeight = 24;

        public const int CanvasWidth = 72;
        public const int CanvasHeight = 40 + (BlockHeight * BuildingRules.MaxHeight);

        /// <summary>The block columns standing on a map, one picture per tile.</summary>
        public static List<ArtPiece> Pieces(TownWorld world, Construction construction, TileMap map, Palette palette)
        {
            var pieces = new List<ArtPiece>();
            if (construction == null)
            {
                return pieces;
            }

            foreach (var project in construction.Projects.Values.OrderBy(p => p.PlotId, System.StringComparer.Ordinal))
            {
                if (!world.TryGetObject(project.PlotId, out var plot) || plot.Location.MapId != map.Id)
                {
                    continue;
                }

                foreach (var column in project.PlacedCells.GroupBy(c => (c.X, c.Y)).OrderBy(g => g.Key.Y).ThenBy(g => g.Key.X))
                {
                    var tile = BuildingRules.TileOf(plot, column.First());
                    var canvas = Column(column.OrderBy(c => c.Level).Select(c => c.Material).ToList(), palette, tile.Position.X, tile.Position.Y);
                    pieces.Add(new ArtPiece($"Blocks {tile.Position.X},{tile.Position.Y}", canvas, tile.Position.X, tile.Position.Y, tile.Position.X, tile.Position.Y, ArtLayer.Solid));
                }
            }

            return pieces;
        }

        /// <summary>One column of blocks, bottom first, anchored on the centre of its tile.</summary>
        public static PixelCanvas Column(IReadOnlyList<MaterialKind> blocks, Palette palette, int x = 0, int y = 0)
        {
            var canvas = new PixelCanvas(CanvasWidth, CanvasHeight, CanvasWidth / 2.0, CanvasHeight - 16);
            var p = new IsoPainter(canvas);
            for (var level = 0; level < blocks.Count; level++)
            {
                var (top, left, right, fleck) = ColoursOf(blocks[level]);
                var z = level * BlockHeight;
                p.Box(0, 0, z, 1, 1, BlockHeight, top, left, right);
                var seed = Noise.Hash(x, y, level);
                p.Top(0, 0, z + BlockHeight, 1, 1, (s, t) => Noise.Value((int)(s * 4), (int)(t * 4), (int)(seed & 0xffff)) < 0.25 ? fleck : top);
                if (level == blocks.Count - 1 && palette.IsWinter)
                {
                    p.Top(0.06, 0.06, z + BlockHeight + 0.5, 0.88, 0.88, (s, t) => Palette.Snow);
                }
            }

            canvas.Outline(Palette.Outline);
            return canvas;
        }

        /// <summary>Top, left face, right face and fleck colours for a block of each material, all from the locked palette.</summary>
        public static (Rgba top, Rgba left, Rgba right, Rgba fleck) ColoursOf(MaterialKind m)
        {
            switch (m)
            {
                case MaterialKind.Wood:
                    return (Palette.WoodLight, Palette.Wood, Palette.WoodDark, Palette.Wood);
                case MaterialKind.Stone:
                    return (Palette.SlateLight, Palette.Stone, Palette.StoneDark, Palette.Stone);
                case MaterialKind.Hardwood:
                    return (Palette.Ebony.Shade(1.35), Palette.Ebony, Palette.Ebony.Shade(0.7), Palette.Ebony);
                case MaterialKind.Iron:
                    return (Palette.Metal, Palette.Metal.Shade(0.75), Palette.Metal.Shade(0.55), Palette.White);
                case MaterialKind.Heartwood:
                    return (Palette.Pumpkin.Shade(0.75), Palette.Pumpkin.Shade(0.55), Palette.Pumpkin.Shade(0.4), Palette.Bread);
                default:
                    return (Palette.GlassLight, Palette.Glass, Palette.Glass.Shade(0.7), Palette.White);
            }
        }
    }
}
