using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Building;
using TinyCity.Simulation.Economy;
using TinyCity.Simulation.World;

namespace TinyCity.Art
{
    /// <summary>
    /// Tiny City 2's building blocks: each block is a quarter of a tile (a square of the ground), so each tile's picture
    /// holds up to four columns of cubes. A finished home's bed is drawn here too.
    /// </summary>
    public static class BlockArt
    {
        /// <summary>How tall one block is drawn, in pixels: half a tile's 24, as the block is half a tile wide.</summary>
        public const int BlockHeight = 12;

        public const int CanvasWidth = 72;
        public const int CanvasHeight = 40 + (BlockHeight * BuildingRules.MaxHeight);

        /// <summary>The blocks standing on a map, one picture per tile, and the beds of finished homes.</summary>
        public static List<ArtPiece> Pieces(TownWorld world, Construction construction, TileMap map, Palette palette)
        {
            var pieces = new List<ArtPiece>();
            if (construction == null)
            {
                return pieces;
            }

            var per = BuildingRules.CellsPerTile;
            foreach (var project in construction.Projects.Values.OrderBy(p => p.PlotId, System.StringComparer.Ordinal))
            {
                if (!world.TryGetObject(project.PlotId, out var plot) || plot.Location.MapId != map.Id)
                {
                    continue;
                }

                foreach (var tileCells in project.PlacedCells.GroupBy(c => (x: c.X / per, y: c.Y / per)).OrderBy(g => g.Key.y).ThenBy(g => g.Key.x))
                {
                    var tile = BuildingRules.TileOf(plot, tileCells.First());
                    var canvas = Tile(tileCells.ToList(), palette);
                    pieces.Add(new ArtPiece($"Blocks {tile.Position.X},{tile.Position.Y}", canvas, tile.Position.X, tile.Position.Y, tile.Position.X, tile.Position.Y, ArtLayer.Solid));
                }

                if (world.TryGetObject(Construction.BedIdOf(project.PlotId), out var bed) && bed.Location.MapId == map.Id)
                {
                    var p = bed.Location.Position;
                    pieces.Add(new ArtPiece(bed.Name, FurnitureArt.Render(ObjectType.Bed, Noise.Of(bed.Id), palette, false), p.X, p.Y, p.X, p.Y, ArtLayer.Solid));
                }
            }

            return pieces;
        }

        /// <summary>The blocks on one tile, anchored on the tile's centre: each quarter's column bottom first, the back quarter first.</summary>
        public static PixelCanvas Tile(IReadOnlyList<BlockCell> cells, Palette palette)
        {
            var canvas = new PixelCanvas(CanvasWidth, CanvasHeight, CanvasWidth / 2.0, CanvasHeight - 16);
            var p = new IsoPainter(canvas);
            var per = BuildingRules.CellsPerTile;
            var size = 1.0 / per;
            foreach (var column in cells.GroupBy(c => (c.X, c.Y)).OrderBy(g => (g.Key.X % per) + (g.Key.Y % per)).ThenBy(g => g.Key.Y))
            {
                var u = (column.Key.X % per) * size;
                var v = (column.Key.Y % per) * size;
                var top = column.Max(c => c.Level);
                foreach (var cell in column.OrderBy(c => c.Level))
                {
                    var (lit, left, right, fleck) = ColoursOf(cell.Material);
                    var z = cell.Level * BlockHeight;
                    p.Box(u, v, z, size, size, BlockHeight, lit, left, right);
                    var seed = Noise.Hash(cell.X, cell.Y, cell.Level);
                    p.Top(u, v, z + BlockHeight, size, size, (s, t) => Noise.Value((int)(s * 2), (int)(t * 2), (int)(seed & 0xffff)) < 0.25 ? fleck : lit);
                    if (cell.Level == top && palette.IsWinter)
                    {
                        p.Top(u + 0.03, v + 0.03, z + BlockHeight + 0.5, size - 0.06, size - 0.06, (s, t) => Palette.Snow);
                    }
                }
            }

            canvas.Outline(Palette.Outline);
            return canvas;
        }

        /// <summary>One column of blocks a tile wide, bottom first: a whole tile of each material's block, for previews.</summary>
        public static PixelCanvas Column(IReadOnlyList<MaterialKind> blocks, Palette palette)
        {
            var cells = new List<BlockCell>();
            for (var level = 0; level < blocks.Count; level++)
            {
                for (var q = 0; q < BuildingRules.CellsPerTile * BuildingRules.CellsPerTile; q++)
                {
                    cells.Add(new BlockCell(q % BuildingRules.CellsPerTile, q / BuildingRules.CellsPerTile, level, blocks[level]));
                }
            }

            return Tile(cells, palette);
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
