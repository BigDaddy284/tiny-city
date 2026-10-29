using System;
using TinyCity.Simulation.World;

namespace TinyCity.Art
{
    /// <summary>
    /// Paints the flat ground of a whole map as one picture: grass, paths, the pond, garden beds and floors,
    /// standing on a shallow block of earth so each map looks like a little diorama.
    /// </summary>
    public static class GroundArt
    {
        /// <summary>Depth of the earth (or floorboards) under a map, in pixels.</summary>
        public const int BaseDepth = 10;

        /// <summary>Ground is worked out on a grid this many times finer than tiles, so edges can wander.</summary>
        private const int Cells = 16;

        /// <summary>How far into an interior's outer tiles its floor begins: the outside face of its walls.</summary>
        public const double InteriorEdge = 0.45;

        private enum Ground : byte
        {
            None,
            Grass,
            Path,
            Soil,
            Water,
            Step,
            Floor,
            Building,
        }

        /// <summary>The canvas for a map's ground. Its anchor is the centre of tile (0, 0).</summary>
        public static PixelCanvas Render(TileMap map, PlaceKind kind, Palette palette)
        {
            var w = map.Width;
            var h = map.Height;
            var canvas = new PixelCanvas((w + h) * IsoPainter.HalfTileWidth, ((w + h) * IsoPainter.HalfTileHeight) + BaseDepth + 1, h * IsoPainter.HalfTileWidth, IsoPainter.HalfTileHeight);
            var grid = BuildGrid(map);
            var painter = new IsoPainter(canvas);
            var seed = Noise.Of(map.Id);

            // The sides of the block the map stands on.
            var exterior = map.IsExterior;
            var lo = exterior ? 0 : InteriorEdge;
            var hiU = exterior ? w : w - InteriorEdge;
            var hiV = exterior ? h : h - InteriorEdge;
            var side = exterior ? Palette.Earth : Palette.WoodDark;
            painter.LeftWall(lo, hiV, -BaseDepth, hiU - lo, BaseDepth, (s, t) => Strata(side, s * (hiU - lo), t, IsoPainter.LeftLight, seed));
            painter.RightWall(hiU, lo, -BaseDepth, hiV - lo, BaseDepth, (s, t) => Strata(side, s * (hiV - lo), t, IsoPainter.RightLight, seed));

            for (var py = 0; py < canvas.Height; py++)
            {
                for (var px = 0; px < canvas.Width; px++)
                {
                    var a = (px + 0.5 - canvas.AnchorX) / IsoPainter.HalfTileWidth;
                    var b = ((py + 0.5 - canvas.AnchorY) / IsoPainter.HalfTileHeight) + 1;
                    var u = (a + b) / 2;
                    var v = (b - a) / 2;
                    if (u < lo || v < lo || u >= hiU || v >= hiV)
                    {
                        continue;
                    }

                    var cu = (int)(u * Cells);
                    var cv = (int)(v * Cells);
                    var colour = exterior
                        ? Outdoor(grid, w * Cells, h * Cells, cu, cv, u, v, px, py, palette, seed)
                        : Indoor(map, kind, u, v, px, py);
                    canvas.Set(px, py, colour);
                }
            }

            return canvas;
        }

        private static Ground[,] BuildGrid(TileMap map)
        {
            var seed = Noise.Of(map.Id);
            var grid = new Ground[map.Width * Cells, map.Height * Cells];
            for (var cv = 0; cv < map.Height * Cells; cv++)
            {
                for (var cu = 0; cu < map.Width * Cells; cu++)
                {
                    var u = (cu + 0.5) / Cells;
                    var v = (cv + 0.5) / Cells;
                    if (map.IsExterior)
                    {
                        // Wander the edges between kinds of ground so paths and the pond look hand-drawn.
                        u += (Noise.Smooth(u * 2.5, v * 2.5, seed) - 0.5) * 0.36;
                        v += (Noise.Smooth(u * 2.5, v * 2.5, seed + 7) - 0.5) * 0.36;
                    }

                    var x = Math.Max(0, Math.Min(map.Width - 1, (int)Math.Floor(u)));
                    var y = Math.Max(0, Math.Min(map.Height - 1, (int)Math.Floor(v)));
                    grid[cu, cv] = Kind(map.GetTile(new GridPos(x, y)));
                }
            }

            return grid;
        }

        private static Ground Kind(TileType type)
        {
            switch (type)
            {
                case TileType.Path: return Ground.Path;
                case TileType.Garden: return Ground.Soil;
                case TileType.Water: return Ground.Water;
                case TileType.Door: return Ground.Step;
                case TileType.Floor: return Ground.Floor;
                case TileType.Wall: return Ground.Floor;
                case TileType.House: return Ground.Building;
                default: return Ground.Grass;
            }
        }

        private static Ground At(Ground[,] grid, int gw, int gh, int cu, int cv)
        {
            return cu < 0 || cv < 0 || cu >= gw || cv >= gh ? Ground.None : grid[cu, cv];
        }

        private static bool Near(Ground[,] grid, int gw, int gh, int cu, int cv, int reach, Ground other, bool not = false)
        {
            for (var dv = -reach; dv <= reach; dv += reach)
            {
                for (var du = -reach; du <= reach; du += reach)
                {
                    var g = At(grid, gw, gh, cu + du, cv + dv);
                    if (g != Ground.None && (not ? g != other : g == other))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static Rgba Outdoor(Ground[,] grid, int gw, int gh, int cu, int cv, double u, double v, int px, int py, Palette p, int seed)
        {
            var g = At(grid, gw, gh, cu, cv);
            var fine = Noise.Value(px >> 1, py, seed);
            Rgba colour;
            switch (g)
            {
                case Ground.Path:
                case Ground.Step:
                    colour = Cobbles(p, u, v, fine, seed);
                    if (Near(grid, gw, gh, cu, cv, 1, Ground.Grass) || Near(grid, gw, gh, cu, cv, 1, Ground.Soil))
                    {
                        // A kerb lit with the season's neon.
                        colour = p.Accent;
                    }
                    else if (Near(grid, gw, gh, cu, cv, 2, Ground.Grass) || Near(grid, gw, gh, cu, cv, 2, Ground.Soil))
                    {
                        colour = p.PathDark;
                    }

                    break;
                case Ground.Water:
                    colour = Water(grid, gw, gh, cu, cv, px, py, p, seed);
                    break;
                case Ground.Soil:
                    colour = Soil(u, v, fine, cu, cv, p, seed);
                    if (Near(grid, gw, gh, cu, cv, 1, Ground.Soil, true))
                    {
                        colour = Palette.WoodDark;
                    }

                    break;
                default:
                    colour = Grass(cu, cv, u, v, fine, p, seed);
                    break;
            }

            // Buildings throw a soft shadow down and to the right; the light comes from the upper left.
            if (g != Ground.Building && At(grid, gw, gh, cu - 9, cv - 3) == Ground.Building)
            {
                colour = colour.Mix(Palette.ShadowTint, 0.22);
            }

            return colour;
        }

        private static Rgba Grass(int cu, int cv, double u, double v, double fine, Palette p, int seed)
        {
            // Paving plates, half a tile each, with dark seams and the odd lighter plate.
            var pu = (u * 2) - Math.Floor(u * 2);
            var pv = (v * 2) - Math.Floor(v * 2);
            var plate = Noise.Value((int)Math.Floor(u * 2), (int)Math.Floor(v * 2), seed + 3);
            var colour = plate < 0.2 ? p.GrassDark.Mix(p.Grass, 0.5) : plate > 0.85 ? p.Grass.Mix(p.GrassLight, 0.6) : p.Grass;
            if (pu < 0.05 || pv < 0.05)
            {
                colour = p.GrassDark;
            }
            else if (fine > 0.97)
            {
                colour = p.GrassLight;
            }

            var dab = Noise.Value(cu >> 1, cv >> 1, seed + 11);
            if (dab < 0.004)
            {
                colour = p.Flowers[(int)(Noise.Value(cu >> 1, cv >> 1, seed + 12) * p.Flowers.Length)];
            }

            return colour;
        }

        private static Rgba Cobbles(Palette p, double u, double v, double fine, int seed)
        {
            // Rounded stones in staggered rows, each a slightly different shade.
            var row = (int)Math.Floor(v * 3);
            var col = (int)Math.Floor((u * 3) + (row % 2 == 0 ? 0 : 0.5));
            var su = ((u * 3) + (row % 2 == 0 ? 0 : 0.5)) - col;
            var sv = (v * 3) - row;
            var edge = Math.Min(Math.Min(su, 1 - su), Math.Min(sv, 1 - sv));
            if (edge < 0.0)
            {
                return p.PathDark;
            }

            // Smooth tarmac with a fine grain.
            var colour = p.Path.Shade(0.97 + (fine * 0.06));
            return colour;
        }

        private static Rgba Water(Ground[,] grid, int gw, int gh, int cu, int cv, int px, int py, Palette p, int seed)
        {
            if (Near(grid, gw, gh, cu, cv, 1, Ground.Water, true))
            {
                return p.IsWinter ? Palette.SnowShade : Palette.Sand;
            }

            var shallow = Near(grid, gw, gh, cu, cv, 3, Ground.Water, true);
            var deep = !Near(grid, gw, gh, cu, cv, 7, Ground.Water, true);
            var colour = shallow ? p.Water.Shade(1.12) : deep ? p.Water.Shade(0.86) : p.Water;
            if (p.IsWinter)
            {
                // Ice: pale with a few dark cracks.
                return Noise.Value(px / 5, py / 2, seed + 21) < 0.04 ? Palette.IceCrack : colour;
            }

            // Short ripple highlights.
            return Noise.Value(px / 6, py / 2, seed + 20) < 0.06 ? colour.Mix(Palette.Highlight, 0.55) : colour;
        }

        private static Rgba Soil(double u, double v, double fine, int cu, int cv, Palette p, int seed)
        {
            // Furrows run along u; crops grow on the ridges and change with the seasons.
            var ridge = ((v * 4) - Math.Floor(v * 4)) < 0.5;
            var colour = ridge ? Palette.Earth : Palette.EarthDark;
            if (p.IsWinter)
            {
                return ridge ? Palette.Snow : Palette.EarthDark.Mix(Palette.SnowShade, 0.4);
            }

            if (ridge)
            {
                var plant = Noise.Value(cu >> 2, cv >> 1, seed + 30);
                var leafy = p.Season == Simulation.Time.Season.Spring ? 0.35 : 0.6;
                if (plant < leafy)
                {
                    colour = fine < 0.5 ? p.LeavesDark : p.Leaves;
                    if (p.Season != Simulation.Time.Season.Spring && plant < 0.08)
                    {
                        colour = p.Accent;
                    }
                }
            }

            return colour;
        }

        private static Rgba Indoor(TileMap map, PlaceKind kind, double u, double v, int px, int py)
        {
            switch (kind)
            {
                case PlaceKind.Store: return Checker(u, v, Palette.StoreFloor[0], Palette.StoreFloor[1]);
                case PlaceKind.Clinic: return Checker(u, v, Palette.ClinicFloor[0], Palette.ClinicFloor[1]);
                case PlaceKind.Bakery: return Checker(u, v, Palette.BakeryFloor[0], Palette.BakeryFloor[1]);
                case PlaceKind.Cafe: return Planks(map, u, v, px, py, Palette.CafeFloor);
                case PlaceKind.Shop: return Checker(u, v, Palette.ShopFloor[0], Palette.ShopFloor[1]);
                default:
                    return Planks(map, u, v, px, py, Palette.CottageFloors[PlaceStyle.Variant(map.Id) % Palette.CottageFloors.Length]);
            }
        }

        private static Rgba Checker(double u, double v, Rgba a, Rgba b)
        {
            var su = (u * 2) - Math.Floor(u * 2);
            var sv = (v * 2) - Math.Floor(v * 2);
            var colour = (((int)Math.Floor(u * 2) + (int)Math.Floor(v * 2)) & 1) == 0 ? a : b;
            return su < 0.06 || sv < 0.06 ? colour.Shade(0.9) : colour;
        }

        private static Rgba Planks(TileMap map, double u, double v, int px, int py, Rgba wood)
        {
            var row = (int)Math.Floor(v * 4);
            var offset = Noise.Value(row, 0, Noise.Of(map.Id)) * 3;
            var along = (u + offset) / 1.5;
            var plank = (int)Math.Floor(along);
            var seam = (v * 4) - row < 0.12 || along - plank < 0.03;
            var colour = wood.Shade(0.93 + (Noise.Value(plank, row, 9) * 0.12));
            if (Noise.Value(px >> 2, py, row) < 0.05)
            {
                colour = colour.Shade(0.92);
            }

            return seam ? wood.Shade(0.72) : colour;
        }

        private static Rgba Strata(Rgba colour, double along, double up, double light, int seed)
        {
            var band = up > 0.7 ? 1.08 : up > 0.35 ? 1.0 : 0.9;
            var speck = Noise.Value((int)(along * 12), (int)(up * 5), seed) < 0.12 ? 0.9 : 1.0;
            return colour.Shade(light * band * speck);
        }
    }
}
