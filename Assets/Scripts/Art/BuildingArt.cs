using System;
using System.Collections.Generic;
using System.Linq;
using TinyTown.Simulation.World;

namespace TinyTown.Art
{
    /// <summary>A building in the village: a block of house tiles and the door that leads inside.</summary>
    public sealed class BuildingPlan
    {
        public BuildingPlan(int x, int y, int width, int depth, GridPos door, PlaceKind kind, int variant, string interiorMapId)
        {
            X = x;
            Y = y;
            Width = width;
            Depth = depth;
            Door = door;
            Kind = kind;
            Variant = variant;
            InteriorMapId = interiorMapId;
        }

        /// <summary>The footprint's corner tile (smallest x and y).</summary>
        public int X { get; }

        public int Y { get; }

        /// <summary>Footprint size along the map's x.</summary>
        public int Width { get; }

        /// <summary>Footprint size along the map's y.</summary>
        public int Depth { get; }

        public GridPos Door { get; }

        public PlaceKind Kind { get; }

        public int Variant { get; }

        /// <summary>The map the door leads to, or null for a building with no way in.</summary>
        public string InteriorMapId { get; }

        public bool Contains(int x, int y) => x >= X && y >= Y && x < X + Width && y < Y + Depth;
    }

    /// <summary>Draws cottages and shops, and cuts them into strips that sort correctly with people walking around them.</summary>
    public static class BuildingArt
    {
        private const int Margin = 10;
        private const int Headroom = 124;
        private const double Inset = 0.12;

        /// <summary>Finds every block of house tiles on a map, with its door and the place inside.</summary>
        public static IReadOnlyList<BuildingPlan> Find(TownWorld world, TileMap map)
        {
            var result = new List<BuildingPlan>();
            var seen = new bool[map.Width, map.Height];
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    if (seen[x, y] || map.GetTile(new GridPos(x, y)) != TileType.House)
                    {
                        continue;
                    }

                    // Flood the block, taking in its door tiles.
                    var tiles = new List<GridPos>();
                    var queue = new Queue<GridPos>();
                    queue.Enqueue(new GridPos(x, y));
                    seen[x, y] = true;
                    while (queue.Count > 0)
                    {
                        var p = queue.Dequeue();
                        tiles.Add(p);
                        foreach (var n in new[] { new GridPos(p.X + 1, p.Y), new GridPos(p.X - 1, p.Y), new GridPos(p.X, p.Y + 1), new GridPos(p.X, p.Y - 1) })
                        {
                            if (!map.InBounds(n) || seen[n.X, n.Y])
                            {
                                continue;
                            }

                            var t = map.GetTile(n);
                            if (t == TileType.House || (t == TileType.Door && map.GetTile(p) == TileType.House))
                            {
                                seen[n.X, n.Y] = true;
                                if (t == TileType.House)
                                {
                                    queue.Enqueue(n);
                                }
                                else
                                {
                                    tiles.Add(n);
                                }
                            }
                        }
                    }

                    var minX = tiles.Min(t => t.X);
                    var minY = tiles.Min(t => t.Y);
                    var maxX = tiles.Max(t => t.X);
                    var maxY = tiles.Max(t => t.Y);
                    var doors = tiles.Where(t => map.GetTile(t) == TileType.Door).ToList();
                    var door = doors.Count > 0 ? doors.OrderByDescending(d => d.Y).ThenBy(d => d.X).First() : new GridPos((minX + maxX) / 2, maxY);
                    var portal = world?.Portals.FirstOrDefault(p => p.From.MapId == map.Id && p.From.Position.Equals(door));
                    var inside = portal?.To.MapId;
                    var kind = PlaceKind.Cottage;
                    if (inside != null && world.TryGetMap(inside, out var interior))
                    {
                        kind = PlaceStyle.Of(world, interior);
                    }

                    result.Add(new BuildingPlan(minX, minY, maxX - minX + 1, maxY - minY + 1, door, kind, PlaceStyle.Variant(inside ?? $"{map.Id}:{minX},{minY}"), inside));
                }
            }

            return result;
        }

        /// <summary>The canvas a building is drawn on. Its anchor is the centre of the footprint's corner tile.</summary>
        public static PixelCanvas NewCanvas(BuildingPlan plan)
        {
            var w = ((plan.Width + plan.Depth) * IsoPainter.HalfTileWidth) + (Margin * 2);
            var h = Headroom + ((plan.Width + plan.Depth - 1) * IsoPainter.HalfTileHeight) + Margin;
            return new PixelCanvas(w, h, (plan.Depth * IsoPainter.HalfTileWidth) + Margin, Headroom);
        }

        /// <summary>The building by day, and the light in its windows for night (drawn on top, fading in at dusk).</summary>
        public static (PixelCanvas body, PixelCanvas glow) Render(BuildingPlan plan, Palette palette)
        {
            var body = NewCanvas(plan);
            var glow = NewCanvas(plan);
            var look = Look.For(plan, palette);
            Draw(new IsoPainter(body), new IsoPainter(glow), plan, look, palette);
            return (body, glow);
        }

        /// <summary>
        /// Cuts a building picture into 32-pixel columns. Each column is drawn in front of whatever stands behind the
        /// front-most footprint tile in that column, so people walk behind and in front of the building correctly.
        /// </summary>
        public static IReadOnlyList<(PixelCanvas strip, int tileX, int tileY)> Strips(PixelCanvas picture, BuildingPlan plan)
        {
            var result = new List<(PixelCanvas, int, int)>();
            var first = (int)Math.Floor(-picture.AnchorX / IsoPainter.HalfTileWidth);
            var last = (int)Math.Ceiling((picture.Width - picture.AnchorX) / IsoPainter.HalfTileWidth);
            for (var k = first; k <= last; k++)
            {
                var left = (int)Math.Round(picture.AnchorX + (k * IsoPainter.HalfTileWidth));
                var x0 = Math.Max(0, left);
                var x1 = Math.Min(picture.Width, left + IsoPainter.HalfTileWidth);
                if (x1 <= x0)
                {
                    continue;
                }

                var strip = picture.Crop(x0, 0, x1 - x0, picture.Height);
                if (strip.IsEmpty())
                {
                    continue;
                }

                var (i, j) = FrontTile(plan, k);
                result.Add((strip, plan.X + i, plan.Y + j));
            }

            return result;
        }

        /// <summary>The footprint tile (relative to its corner) nearest the viewer among those under strip <paramref name="k"/>.</summary>
        public static (int i, int j) FrontTile(BuildingPlan plan, int k)
        {
            var best = (i: 0, j: 0);
            var bestSum = int.MinValue;
            var lo = -(plan.Depth - 1);
            var hi = plan.Width - 1;
            foreach (var raw in new[] { k, k + 1 })
            {
                var d = Math.Max(lo, Math.Min(hi, raw));
                var i = Math.Min(plan.Width - 1, plan.Depth - 1 + d);
                var j = i - d;
                if (i + j > bestSum)
                {
                    bestSum = i + j;
                    best = (i, j);
                }
            }

            return best;
        }

        private enum OpeningKind
        {
            Window,
            ShopWindow,
            Door,
        }

        private readonly struct Opening
        {
            public Opening(OpeningKind kind, double along, double width, double bottom, double top)
            {
                Kind = kind;
                Along = along;
                Width = width;
                Bottom = bottom;
                Top = top;
            }

            public OpeningKind Kind { get; }

            public double Along { get; }

            public double Width { get; }

            public double Bottom { get; }

            public double Top { get; }

            public bool Contains(double a, double z) => Math.Abs(a - Along) <= Width / 2 && z >= Bottom && z <= Top;
        }

        private sealed class Look
        {
            public Rgba Wall;
            public Rgba Trim;
            public Rgba Roof;
            public Rgba DoorColour;
            public double WallHeight;
            public double RoofRise;
            public bool Thatch;
            public bool Timber;
            public Rgba? AwningA;
            public Rgba AwningB;
            public PlaceKind Kind;

            public static Look For(BuildingPlan plan, Palette p)
            {
                var v = plan.Variant;
                var look = new Look
                {
                    Wall = Palette.CottageWalls[v % Palette.CottageWalls.Length],
                    Roof = Palette.CottageRoofs[(v / 7) % Palette.CottageRoofs.Length],
                    DoorColour = Palette.CottageDoors[(v / 3) % Palette.CottageDoors.Length],
                    Trim = Palette.White,
                    WallHeight = 42,
                    RoofRise = 42,
                    Timber = (v / 11) % 3 == 0,
                    Kind = plan.Kind,
                };
                look.Thatch = look.Roof.Equals(Palette.Thatch);
                switch (plan.Kind)
                {
                    case PlaceKind.Store:
                        look.Wall = Palette.StoreWall;
                        look.Roof = Palette.StoreRoof;
                        look.DoorColour = Palette.StoreDoor;
                        look.AwningA = Palette.StoreAwning;
                        look.AwningB = Palette.White;
                        break;
                    case PlaceKind.Cafe:
                        look.Wall = Palette.CafeWall;
                        look.Roof = Palette.CafeRoof;
                        look.DoorColour = Palette.CafeDoor;
                        look.AwningA = Palette.CafeAwning;
                        look.AwningB = Palette.White;
                        break;
                    case PlaceKind.Bakery:
                        look.Wall = Palette.BakeryWall;
                        look.Roof = Palette.BakeryRoof;
                        look.DoorColour = Palette.BakeryDoor;
                        look.AwningA = Palette.BakeryAwning;
                        look.AwningB = Palette.BakeryAwningStripe;
                        break;
                    case PlaceKind.Clinic:
                        look.Wall = Palette.ClinicWall;
                        look.Roof = Palette.ClinicRoof;
                        look.DoorColour = Palette.ClinicDoor;
                        break;
                    case PlaceKind.Shop:
                        look.AwningA = Palette.ShopAwning;
                        look.AwningB = Palette.White;
                        break;
                }

                if (plan.Kind != PlaceKind.Cottage)
                {
                    look.WallHeight = 50;
                    look.RoofRise = 38;
                    look.Thatch = false;
                    look.Timber = false;
                }

                return look;
            }
        }

        private static void Draw(IsoPainter painter, IsoPainter glow, BuildingPlan plan, Look look, Palette palette)
        {
            var u0 = Inset;
            var u1 = plan.Width - Inset;
            var v0 = Inset;
            var v1 = plan.Depth - Inset;
            var vm = plan.Depth / 2.0;
            var h = look.WallHeight;
            var rise = look.RoofRise;
            const double overhangU = 0.22;
            const double overhangV = 0.28;
            var front = FrontOpenings(plan, look, u0, u1);
            var side = SideOpenings(plan, look, v0, v1);

            // Back slope first, so the front slope and gable can cover it.
            painter.Face((u0 - overhangU, vm, h + rise), (u1 - u0 + (2 * overhangU), 0, 0), (0, v0 - overhangV - vm, -(rise + 4)), (s, t) => RoofTile(look, palette, s, 1 - t, 0.78, plan.Variant));

            // Chimney behind the ridge on cottages.
            if (look.Kind == PlaceKind.Cottage)
            {
                var cu = plan.Width * (0.62 + (Noise.Value(plan.Variant, 5) * 0.15));
                painter.Box(cu, vm - 0.75, h, 0.45, 0.42, rise + 16, Palette.Brick);
                painter.Box(cu - 0.04, vm - 0.79, h + rise + 16, 0.53, 0.5, 4, Palette.StoneDark);
                if (palette.IsWinter)
                {
                    painter.Top(cu - 0.04, vm - 0.79, h + rise + 20, 0.53, 0.5, (s, t) => Palette.Snow);
                }
            }

            // Walls.
            painter.RightWall(u1, v0, 0, v1 - v0, h, (s, t) => WallPixel(look, palette, front: false, along: v0 + (s * (v1 - v0)), z: t * h, s, side, plan.Variant, IsoPainter.RightLight));
            painter.LeftWall(u0, v1, 0, u1 - u0, h, (s, t) => WallPixel(look, palette, front: true, along: u0 + (s * (u1 - u0)), z: t * h, s, front, plan.Variant, IsoPainter.LeftLight));
            glow.RightWall(u1, v0, 0, v1 - v0, h, (s, t) => GlowPixel(v0 + (s * (v1 - v0)), t * h, side));
            glow.LeftWall(u0, v1, 0, u1 - u0, h, (s, t) => GlowPixel(u0 + (s * (u1 - u0)), t * h, front));

            // Gable end above the right wall.
            painter.Face((u1, v0, h), (0, v1 - v0, 0), (0, 0, rise), (s, t) =>
            {
                if (t > 1 - Math.Abs((2 * s) - 1))
                {
                    return Rgba.Clear;
                }

                var colour = look.Wall.Shade(IsoPainter.RightLight * 0.96);
                if (look.Timber && (Math.Abs(s - 0.5) < 0.02 || t < 0.04))
                {
                    colour = Palette.WoodDark;
                }

                // A little round vent.
                var dx = (s - 0.5) * (v1 - v0) * 32;
                var dy = (t - 0.45) * rise;
                return (dx * dx) + (dy * dy) < 16 ? (look.Kind == PlaceKind.Cottage ? Palette.Glass : look.Trim) : colour;
            });

            // Front slope, ridge to front eave.
            painter.Face((u0 - overhangU, v1 + overhangV, h - 4), (u1 - u0 + (2 * overhangU), 0, 0), (0, vm - v1 - overhangV, rise + 4), (s, t) =>
            {
                var colour = RoofTile(look, palette, s, t, IsoPainter.LeftLight * 1.05, plan.Variant);
                if (s > 0.985)
                {
                    colour = colour.Shade(0.7);
                }

                return colour;
            });

            if (look.AwningA.HasValue)
            {
                Awning(painter, look, u0 + 0.25, u1 - 0.25, v1, h, palette);
                Sign(painter, look, plan.Door.X - plan.X + 0.5, v1, h);
            }
            else if (look.Kind == PlaceKind.Clinic)
            {
                Sign(painter, look, plan.Door.X - plan.X + 0.5, v1, h);
            }

            FlowerBoxes(painter, palette, front, v1, look);
        }

        private static List<Opening> FrontOpenings(BuildingPlan plan, Look look, double u0, double u1)
        {
            var door = plan.Door.X - plan.X + 0.5;
            var list = new List<Opening> { new Opening(OpeningKind.Door, door, 0.5, 0, 30) };
            if (look.Kind == PlaceKind.Cottage)
            {
                foreach (var a in new[] { u0 + 0.75, u1 - 0.75, (u0 + u1) / 2 })
                {
                    if (Math.Abs(a - door) > 0.85)
                    {
                        list.Add(new Opening(OpeningKind.Window, a, 0.44, 15, 31));
                    }
                }
            }
            else
            {
                var leftWidth = door - 0.45 - (u0 + 0.3);
                var rightWidth = u1 - 0.3 - (door + 0.45);
                if (leftWidth > 0.5)
                {
                    list.Add(new Opening(OpeningKind.ShopWindow, u0 + 0.3 + (leftWidth / 2), leftWidth, 8, 32));
                }

                if (rightWidth > 0.5)
                {
                    list.Add(new Opening(OpeningKind.ShopWindow, door + 0.45 + (rightWidth / 2), rightWidth, 8, 32));
                }
            }

            return list;
        }

        private static List<Opening> SideOpenings(BuildingPlan plan, Look look, double v0, double v1)
        {
            var list = new List<Opening>();
            if (v1 - v0 > 2.5)
            {
                list.Add(new Opening(OpeningKind.Window, v0 + ((v1 - v0) * 0.33), 0.42, 15, 31));
                list.Add(new Opening(OpeningKind.Window, v0 + ((v1 - v0) * 0.72), 0.42, 15, 31));
            }
            else
            {
                list.Add(new Opening(OpeningKind.Window, (v0 + v1) / 2, 0.42, 15, 31));
            }

            return list;
        }

        private static Rgba WallPixel(Look look, Palette palette, bool front, double along, double z, double s, List<Opening> openings, int variant, double light)
        {
            foreach (var o in openings)
            {
                var frame = 0.05;
                var inFrame = Math.Abs(along - o.Along) <= (o.Width / 2) + frame && z >= o.Bottom - 2 && z <= o.Top + 2;
                if (!inFrame)
                {
                    continue;
                }

                if (!o.Contains(along, z))
                {
                    return (o.Kind == OpeningKind.Door ? Palette.WoodDark : look.Trim).Shade(light);
                }

                var local = (along - (o.Along - (o.Width / 2))) / o.Width;
                var up = (z - o.Bottom) / (o.Top - o.Bottom);
                if (o.Kind == OpeningKind.Door)
                {
                    var colour = look.DoorColour.Shade(light);
                    if (up > 0.72 && up < 0.92 && local > 0.2 && local < 0.8)
                    {
                        return Palette.Glass.Shade(light * 1.1);
                    }

                    if (Math.Abs(local - 0.78) < 0.07 && Math.Abs(up - 0.45) < 0.05)
                    {
                        return Palette.Brass;
                    }

                    return Math.Abs(local - 0.5) < 0.03 ? colour.Shade(0.85) : colour;
                }

                // Glass with a cross bar and a pale reflection.
                if (Math.Abs(local - 0.5) < (o.Kind == OpeningKind.ShopWindow ? 0.012 : 0.05) || Math.Abs(up - 0.55) < 0.06)
                {
                    return look.Trim.Shade(light);
                }

                var glass = Palette.Glass.Shade(light * 1.15);
                if (Math.Abs(local - up + 0.1) < 0.08 || Math.Abs(local - up - 0.25) < 0.04)
                {
                    glass = glass.Mix(Palette.GlassLight, 0.6);
                }

                if (o.Kind == OpeningKind.ShopWindow && up < 0.3)
                {
                    // Goods on display behind the glass.
                    var item = (int)(local * 9);
                    glass = Noise.Value(item, variant, 3) < 0.6 ? Palette.WindowGoods[item % 3].Shade(light) : glass;
                }

                return glass;
            }

            var wall = look.Wall;
            if (z < 6)
            {
                // Stone footing.
                var row = z < 3 ? 0 : 1;
                var brick = (int)((along * 6) + (row * 0.5));
                wall = Palette.Stone.Shade(0.9 + (Noise.Value(brick, row, variant) * 0.15));
            }
            else if (look.Timber && (s < 0.02 || s > 0.98 || z > look.WallHeight - 3 || (front && Math.Abs(z - 34) < 1.5)))
            {
                wall = Palette.WoodDark;
            }
            else if (look.Kind != PlaceKind.Cottage && z > look.WallHeight - 4)
            {
                wall = look.Trim;
            }
            else if (Noise.Value((int)(along * 40), (int)(z / 2), variant) < 0.05)
            {
                wall = wall.Shade(0.95);
            }

            return wall.Shade(light);
        }

        private static Rgba GlowPixel(double along, double z, List<Opening> openings)
        {
            foreach (var o in openings)
            {
                if (o.Contains(along, z))
                {
                    if (o.Kind == OpeningKind.Door)
                    {
                        var up = (z - o.Bottom) / (o.Top - o.Bottom);
                        var local = (along - (o.Along - (o.Width / 2))) / o.Width;
                        return up > 0.72 && up < 0.92 && local > 0.2 && local < 0.8 ? Palette.WindowGlow : Rgba.Clear;
                    }

                    return Palette.WindowGlow;
                }
            }

            return Rgba.Clear;
        }

        private static Rgba RoofTile(Look look, Palette palette, double s, double t, double light, int variant)
        {
            const int rows = 7;
            var row = Math.Min(rows - 1, (int)(t * rows));
            var frac = (t * rows) - row;
            var tile = (int)Math.Floor((s * 28) + (row % 2 == 0 ? 0 : 0.5));
            Rgba colour;
            if (look.Thatch)
            {
                colour = look.Roof.Shade(0.9 + (Noise.Value((int)(s * 120), row, variant) * 0.2));
                if (frac > 0.85)
                {
                    colour = colour.Shade(0.88);
                }
            }
            else
            {
                colour = look.Roof.Shade(0.92 + (Noise.Value(tile, row, variant) * 0.14));
                var seam = ((s * 28) + (row % 2 == 0 ? 0 : 0.5)) - tile;
                if (frac > 0.82 || seam < 0.06)
                {
                    colour = colour.Shade(0.78);
                }
            }

            if (t < 0.06)
            {
                colour = colour.Shade(0.75);
            }

            if (t > 0.96)
            {
                colour = look.Roof.Shade(0.7);
            }

            if (palette.IsWinter && t > 0.1 + (Noise.Value(tile, 0, variant) * 0.12))
            {
                colour = frac > 0.85 ? Palette.SnowShade : Palette.Snow;
            }

            return colour.Shade(light);
        }

        private static void Awning(IsoPainter painter, Look look, double from, double to, double v1, double h, Palette palette)
        {
            var top = h - 12;
            painter.Face((from, v1, top), (to - from, 0, 0), (0, 0.45, -10), (s, t) =>
            {
                var stripe = ((int)(s * (to - from) * 6) % 2) == 0;
                var colour = stripe ? look.AwningA.Value : look.AwningB;
                if (palette.IsWinter && t < 0.5)
                {
                    colour = Palette.Snow;
                }

                return colour.Shade(t > 0.85 ? 0.75 : IsoPainter.TopLight * 0.95);
            });

            // Scalloped valance hanging from the front edge.
            painter.LeftWall(from, v1 + 0.45, top - 15, to - from, 5, (s, t) =>
            {
                var bump = ((s * (to - from) * 6) % 1) - 0.5;
                if (t < bump * bump * 2.5)
                {
                    return Rgba.Clear;
                }

                var stripe = ((int)(s * (to - from) * 6) % 2) == 0;
                return (stripe ? look.AwningA.Value : look.AwningB).Shade(IsoPainter.LeftLight * 0.9);
            });
        }

        private static void Sign(IsoPainter painter, Look look, double along, double v1, double h)
        {
            // A board above the door with a picture of what is sold inside.
            var z = h - 11;
            var board = look.Kind == PlaceKind.Clinic ? Palette.White : Palette.WoodLight;
            painter.LeftWall(along - 0.45, v1 + 0.02, z, 0.9, 10, (s, t) =>
            {
                if (s < 0.05 || s > 0.95 || t < 0.12 || t > 0.88)
                {
                    return Palette.WoodDark;
                }

                return Icon(look.Kind, s, t) ?? board.Shade(IsoPainter.LeftLight);
            });
        }

        private static Rgba? Icon(PlaceKind kind, double s, double t)
        {
            var x = (s - 0.5) * 9;
            var y = (t - 0.5) * 3;
            switch (kind)
            {
                case PlaceKind.Clinic:
                    return (Math.Abs(x) < 0.35 && Math.Abs(y) < 1.1) || (Math.Abs(x) < 1.1 && Math.Abs(y) < 0.35) ? Palette.SignRed : (Rgba?)null;
                case PlaceKind.Cafe:
                    return (x > -1.1 && x < 0.7 && y > -0.6 && y < 0.9) || (x >= 0.7 && x < 1.2 && Math.Abs(y) < 0.4) ? Palette.White : (Rgba?)null;
                case PlaceKind.Bakery:
                    return ((x * x / 4) + (y * y / 1)) < 1 ? Palette.Bread : (Rgba?)null;
                case PlaceKind.Store:
                    return ((x * x) + (y * y)) < 1 ? Palette.SignRed : Math.Abs(x) < 0.2 && y < -0.9 && y > -1.4 ? Palette.LeafGreen : (Rgba?)null;
                default:
                    return null;
            }
        }

        private static void FlowerBoxes(IsoPainter painter, Palette palette, List<Opening> front, double v1, Look look)
        {
            if (look.Kind != PlaceKind.Cottage)
            {
                return;
            }

            foreach (var o in front)
            {
                if (o.Kind != OpeningKind.Window)
                {
                    continue;
                }

                painter.Box(o.Along - 0.26, v1, o.Bottom - 5, 0.52, 0.1, 3, Palette.Wood);
                var flowers = palette.Flowers;
                painter.Top(o.Along - 0.26, v1, o.Bottom - 2, 0.52, 0.1, (s, t) => Rgba.Clear);
                for (var i = 0; i < 5; i++)
                {
                    var (x, y) = painter.Project(o.Along - 0.2 + (i * 0.1), v1 + 0.05, o.Bottom - 1);
                    var colour = palette.IsWinter ? Palette.Snow : i % 2 == 0 ? palette.Leaves : flowers[i % flowers.Length];
                    painter.Canvas.FillRect((int)x, (int)y - 1, 2, 2, colour);
                }
            }
        }
    }
}
