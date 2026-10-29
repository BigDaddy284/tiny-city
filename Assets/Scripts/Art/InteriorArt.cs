using System;
using TinyCity.Simulation.World;

namespace TinyCity.Art
{
    /// <summary>
    /// The walls of a room, drawn like a doll's house: the two back walls stand tall, the two front walls are cut
    /// down low so the room can be seen, and walls inside the room are half height.
    /// </summary>
    public static class InteriorArt
    {
        public const double Outer = GroundArt.InteriorEdge;
        public const double Inner = 0.7;
        public const int BackHeight = 64;
        public const int FrontHeight = 12;
        public const int InnerHeight = 40;

        /// <summary>The wall standing on one wall or door tile, or null if there is nothing to draw there.</summary>
        public static PixelCanvas Render(TileMap map, PlaceKind kind, int x, int y)
        {
            var type = map.GetTile(new GridPos(x, y));
            if (type != TileType.Wall && type != TileType.Door)
            {
                return null;
            }

            var canvas = FurnitureArt.NewCanvas();
            var p = new IsoPainter(canvas);
            var paper = Wallpaper(kind, map.Id);
            var outside = Palette.FrontWallOutside;
            var w = map.Width;
            var h = map.Height;
            var isEdge = x == 0 || y == 0 || x == w - 1 || y == h - 1;

            if (type == TileType.Door)
            {
                DoorWay(p, map, x, y);
                return canvas;
            }

            if (!isEdge)
            {
                InnerWall(p, map, x, y, paper);
                canvas.Outline(Palette.Outline);
                return canvas;
            }

            // Back walls: the row y = 0 (along u) and the column x = 0 (along v).
            if (y == 0)
            {
                Slab(p, x, y, Outer, w - Outer, Outer, Inner, BackHeight, paper, Palette.WallCap, x, w);
            }

            // The left back wall starts where the right one ends (v = Inner), so the two meet in a clean inside
            // corner instead of the left one overlapping the right.
            if (x == 0)
            {
                Slab(p, x, y, Outer, Inner, Inner, h - Outer, BackHeight, paper, Palette.WallCap, y, h);
            }

            if (y == h - 1)
            {
                Slab(p, x, y, Outer, w - Outer, h - Inner, h - Outer, FrontHeight, outside, Palette.WoodDark, -1, w);
            }

            if (x == w - 1)
            {
                Slab(p, x, y, w - Inner, w - Outer, Outer, h - Outer, FrontHeight, outside, Palette.WoodDark, -1, h);
            }

            canvas.Outline(Palette.Outline);
            return canvas;
        }

        /// <summary>Wall colours inside each kind of place.</summary>
        public static Rgba Wallpaper(PlaceKind kind, string mapId)
        {
            switch (kind)
            {
                case PlaceKind.Store: return Palette.StoreWallpaper;
                case PlaceKind.Cafe: return Palette.CafeWallpaper;
                case PlaceKind.Bakery: return Palette.BakeryWallpaper;
                case PlaceKind.Clinic: return Palette.ClinicWallpaper;
                case PlaceKind.Shop: return Palette.ShopWallpaper;
                case PlaceKind.Bar: return Palette.Charcoal;
                default:
                    return Palette.CottageWallpapers[PlaceStyle.Variant(mapId) % Palette.CottageWallpapers.Length];
            }
        }

        private static bool IsInnerWall(TileMap map, int x, int y)
        {
            return x > 0 && y > 0 && x < map.Width - 1 && y < map.Height - 1 && map.GetTile(new GridPos(x, y)) == TileType.Wall;
        }

        private static bool IsWallish(TileMap map, int x, int y)
        {
            return map.InBounds(new GridPos(x, y)) && map.GetTile(new GridPos(x, y)) == TileType.Wall;
        }

        /// <summary>
        /// Draws the part of a straight wall (in map coordinates) that lies on tile (x, y). <paramref name="index"/>
        /// numbers the tiles along a back wall, so windows and pictures are spaced out; -1 for front walls.
        /// </summary>
        private static void Slab(IsoPainter p, int x, int y, double u0, double u1, double v0, double v1, int height, Rgba face, Rgba cap, int index, int length)
        {
            var cu0 = Math.Max(u0, x) - x;
            var cu1 = Math.Min(u1, x + 1) - x;
            var cv0 = Math.Max(v0, y) - y;
            var cv1 = Math.Min(v1, y + 1) - y;
            if (cu1 <= cu0 || cv1 <= cv0)
            {
                return;
            }

            var alongU = (u1 - u0) >= (v1 - v0);
            var window = index > 0 && index < length - 1 && index % 3 == 1;
            var picture = index > 0 && index < length - 1 && index % 3 == 2 && Noise.Value(index, length, 4) < 0.6;
            p.LeftWall(cu0, cv1, 0, cu1 - cu0, height, (s, t) => WallFace(face, alongU ? s : 0.5, t * height, height, alongU && window, alongU && picture, IsoPainter.LeftLight));
            if (Math.Abs(cu1 + x - u1) < 1e-9)
            {
                p.RightWall(cu1, cv0, 0, cv1 - cv0, height, (s, t) => WallFace(face, alongU ? 0.5 : s, t * height, height, !alongU && window, !alongU && picture, IsoPainter.RightLight));
            }

            p.Top(cu0, cv0, height, cu1 - cu0, cv1 - cv0, (s, t) => cap);
        }

        private static Rgba WallFace(Rgba paper, double s, double z, int height, bool window, bool picture, double light)
        {
            if (height <= FrontHeight)
            {
                return z > height - 3 ? Palette.GlassLight : paper.Shade(light);
            }

            if (z < 4)
            {
                return Palette.Charcoal.Shade(light);
            }

            if (z < 5.5)
            {
                // LED strip along the skirting.
                return Palette.GlassLight;
            }

            if (Math.Abs(z - 24) < 0.9)
            {
                // A neon rail at waist height.
                return Palette.Poppy;
            }

            if (window && s > 0.22 && s < 0.78 && z > 26 && z < 52)
            {
                if (s < 0.25 || s > 0.75 || z < 27.5 || z > 50.5)
                {
                    return Palette.Iron;
                }

                // The city outside: towers against a purple sky, with lit windows.
                var col = (int)(s * 40);
                var tower = 30 + (Noise.Value(col / 3, 7, 11) * 16);
                if (z < tower)
                {
                    return Noise.Value(col, (int)(z / 2), 12) < 0.18 ? Palette.WindowGlow : Palette.Charcoal;
                }

                return z > 44 ? Palette.RoomWindowSkyHigh : Palette.RoomWindowSky;
            }

            if (picture && s > 0.32 && s < 0.68 && z > 31 && z < 47)
            {
                if (s < 0.36 || s > 0.64 || z < 33 || z > 45)
                {
                    return Palette.PictureFrame.Shade(light);
                }

                return (z < 38 ? Palette.PictureLand : Palette.PictureSky).Shade(light);
            }

            // Faint vertical stripes in the wallpaper.
            var stripe = ((int)(s * 8)) % 2 == 0 ? 1.0 : 0.96;
            return paper.Shade(light * stripe * (z < 24 ? 0.94 : 1.0));
        }

        private static void InnerWall(IsoPainter p, TileMap map, int x, int y, Rgba paper)
        {
            p.Box(0.38, 0.38, 0, 0.24, 0.24, InnerHeight, paper.Shade(1.0), paper.Shade(IsoPainter.LeftLight), paper.Shade(IsoPainter.RightLight));
            foreach (var (dx, dy) in new[] { (-1, 0), (0, -1), (1, 0), (0, 1) })
            {
                if (IsWallish(map, x + dx, y + dy))
                {
                    var edge = !IsInnerWall(map, x + dx, y + dy);
                    Arm(p, dx, dy, from: 0.38, paper, edge);
                }
            }

            p.Top(0.38, 0.38, InnerHeight, 0.24, 0.24, (s, t) => Palette.WallCap);
        }

        /// <summary>
        /// A half-height wall from the middle of the tile towards a neighbour. Towards an outer wall it runs on to that
        /// wall's face (<see cref="Inner"/> into the next tile), otherwise to the tile's edge.
        /// </summary>
        private static void Arm(IsoPainter p, int dx, int dy, double from, Rgba paper, bool edge)
        {
            var reach = edge ? 1 - Inner : 0;
            double u0 = 0.38, u1 = 0.62, v0 = 0.38, v1 = 0.62;
            if (dx < 0)
            {
                u0 = -reach;
                u1 = from;
            }
            else if (dx > 0)
            {
                u0 = 1 - from;
                u1 = 1 + reach;
            }
            else if (dy < 0)
            {
                v0 = -reach;
                v1 = from;
            }
            else
            {
                v0 = 1 - from;
                v1 = 1 + reach;
            }

            p.Box(u0, v0, 0, u1 - u0, v1 - v0, InnerHeight, Palette.WallCap, paper.Shade(IsoPainter.LeftLight), paper.Shade(IsoPainter.RightLight));
        }

        private static void DoorWay(IsoPainter p, TileMap map, int x, int y)
        {
            // A doormat and the two posts of the door frame in the low front wall.
            var front = y == map.Height - 1;
            if (front)
            {
                p.Top(0.2, 0.0, 0.5, 0.6, 0.3, (s, t) => (int)(s * 8) % 2 == 0 ? Palette.Doormat : Palette.DoormatDark);
                p.Box(0.0, 1 - Inner, 0, 0.12, Inner - Outer, FrontHeight + 4, Palette.WoodDark);
                p.Box(0.88, 1 - Inner, 0, 0.12, Inner - Outer, FrontHeight + 4, Palette.WoodDark);
            }
            else
            {
                p.Top(0.0, 0.2, 0.5, 0.3, 0.6, (s, t) => (int)(t * 8) % 2 == 0 ? Palette.Doormat : Palette.DoormatDark);
                p.Box(1 - Inner, 0.0, 0, Inner - Outer, 0.12, FrontHeight + 4, Palette.WoodDark);
                p.Box(1 - Inner, 0.88, 0, Inner - Outer, 0.12, FrontHeight + 4, Palette.WoodDark);
            }
        }
    }
}
