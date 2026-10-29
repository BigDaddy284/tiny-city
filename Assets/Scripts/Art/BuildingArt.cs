using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.World;

namespace TinyCity.Art
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
            public Rgba Roof;
            public Rgba DoorColour;
            public double WallHeight;
            /// <summary>A shop's own neon colour (its tube and sign); homes use their door colour.</summary>
            public Rgba? Neon;
            public PlaceKind Kind;

            public static Look For(BuildingPlan plan, Palette p)
            {
                var v = plan.Variant;
                var look = new Look
                {
                    Wall = Palette.CottageWalls[v % Palette.CottageWalls.Length],
                    Roof = Palette.CottageRoofs[(v / 7) % Palette.CottageRoofs.Length],
                    DoorColour = Palette.CottageDoors[(v / 3) % Palette.CottageDoors.Length],
                    WallHeight = 58,
                    Kind = plan.Kind,
                };
                switch (plan.Kind)
                {
                    case PlaceKind.Store:
                        look.Wall = Palette.StoreWall;
                        look.Roof = Palette.StoreRoof;
                        look.DoorColour = Palette.StoreDoor;
                        look.Neon = Palette.StoreNeon;
                        break;
                    case PlaceKind.Cafe:
                        look.Wall = Palette.CafeWall;
                        look.Roof = Palette.CafeRoof;
                        look.DoorColour = Palette.CafeDoor;
                        look.Neon = Palette.CafeNeon;
                        break;
                    case PlaceKind.Bakery:
                        look.Wall = Palette.BakeryWall;
                        look.Roof = Palette.BakeryRoof;
                        look.DoorColour = Palette.BakeryDoor;
                        look.Neon = Palette.BakeryNeon;
                        break;
                    case PlaceKind.Clinic:
                        look.Wall = Palette.ClinicWall;
                        look.Roof = Palette.ClinicRoof;
                        look.DoorColour = Palette.ClinicDoor;
                        break;
                    case PlaceKind.Shop:
                        look.Neon = Palette.ShopNeon;
                        break;
                }

                if (plan.Kind != PlaceKind.Cottage)
                {
                    look.WallHeight = 66;
                }

                return look;
            }
        }

        private static void Draw(IsoPainter painter, IsoPainter glow, BuildingPlan plan, Look look, Palette palette)
        {
            // Tiny City blocks: flat roofs with a neon-edged parapet, rooftop plant and an antenna, strip windows on two
            // floors, sliding glass doors, and neon trim and shop signs that glow at night. Nothing sticks out from the
            // walls, so the building strips sort cleanly with residents walking past.
            var u0 = Inset;
            var u1 = plan.Width - Inset;
            var v0 = Inset;
            var v1 = plan.Depth - Inset;
            var h = look.WallHeight;
            var neon = look.Neon ?? look.DoorColour;
            var front = FrontOpenings(plan, look, u0, u1);
            var side = SideOpenings(plan, look, v0, v1);

            painter.RightWall(u1, v0, 0, v1 - v0, h, (s, t) => WallPixel(look, palette, front: false, along: v0 + (s * (v1 - v0)), z: t * h, s, side, plan.Variant, IsoPainter.RightLight, neon));
            painter.LeftWall(u0, v1, 0, u1 - u0, h, (s, t) => WallPixel(look, palette, front: true, along: u0 + (s * (u1 - u0)), z: t * h, s, front, plan.Variant, IsoPainter.RightLight + 0.16, neon));
            glow.RightWall(u1, v0, 0, v1 - v0, h, (s, t) => GlowPixel(v0 + (s * (v1 - v0)), t * h, side, h, neon, plan.Variant));
            glow.LeftWall(u0, v1, 0, u1 - u0, h, (s, t) => GlowPixel(u0 + (s * (u1 - u0)), t * h, front, h, neon, plan.Variant, look.Neon.HasValue && s > 0.04 && s < 0.96));

            // Flat roof with a low parapet; its outer lip carries a neon line.
            painter.Top(u0, v0, h, u1 - u0, v1 - v0, (s, t) =>
            {
                var c = look.Roof.Shade(0.95 + (Noise.Value((int)(s * 20), (int)(t * 20), plan.Variant) * 0.08));
                if (palette.IsWinter && Noise.Value((int)(s * 30), (int)(t * 30), plan.Variant + 1) < 0.7)
                {
                    c = Palette.Snow.Shade(0.9);
                }

                return c;
            });
            painter.RightWall(u1, v0, h, v1 - v0, 4, (s, t) => (t > 0.5 ? neon : look.Roof.Shade(1.4)).Shade(IsoPainter.RightLight + 0.2));
            painter.LeftWall(u0, v1, h, u1 - u0, 4, (s, t) => (t > 0.5 ? neon : look.Roof.Shade(1.5)).Shade(IsoPainter.LeftLight + 0.16));
            glow.RightWall(u1, v0, h, v1 - v0, 4, (s, t) => t > 0.5 ? neon : Rgba.Clear);
            glow.LeftWall(u0, v1, h, u1 - u0, 4, (s, t) => t > 0.5 ? neon : Rgba.Clear);

            // Rooftop plant: an air unit, a vent and an antenna with a red light, placed by the building's variant.
            var cu = u0 + ((u1 - u0) * (0.25 + (Noise.Value(plan.Variant, 5) * 0.3)));
            var cv = v0 + ((v1 - v0) * 0.3);
            painter.Box(cu, cv, h, 0.7, 0.55, 12, Palette.SlateLight, Palette.Slate, Palette.Charcoal);
            painter.Top(cu + 0.1, cv + 0.08, h + 12, 0.5, 0.4, (s, t) => ((s - 0.5) * (s - 0.5)) + ((t - 0.5) * (t - 0.5)) < 0.12 ? Palette.Iron : Palette.SlateLight);
            painter.Box(u1 - 0.7, v0 + 0.3, h, 0.3, 0.3, 7, Palette.Metal, Palette.Metal.Shade(0.85), Palette.Metal.Shade(0.7));
            var au = u0 + ((u1 - u0) * 0.82);
            var av = v0 + ((v1 - v0) * 0.55);
            painter.Post(au, av, h, 0.03, 30 + (Noise.Value(plan.Variant, 9) * 14), Palette.Metal);
            var (tx, ty) = painter.Project(au, av, h + 30 + (Noise.Value(plan.Variant, 9) * 14));
            painter.Canvas.FillRect((int)tx - 1, (int)ty - 2, 2, 2, Palette.SignRed);
            glow.Canvas.FillRect((int)tx - 1, (int)ty - 2, 2, 2, Palette.SignRed);
            if (look.Kind != PlaceKind.Cottage)
            {
                NeonSign(painter, glow, look, plan.Door.X - plan.X + 0.5, v1, h, neon);
            }
        }

        private static void NeonSign(IsoPainter painter, IsoPainter glow, Look look, double along, double v1, double h, Rgba neon)
        {
            // A sign flush with the wall above the door: a neon frame round the shop's picture.
            const double width = 1.1;
            var bottom = 41.0;
            var height = h - bottom - 5;
            Func<double, double, Rgba?> shade = (s, t) =>
            {
                if (s < 0.05 || s > 0.95 || t < 0.1 || t > 0.9)
                {
                    return neon;
                }

                return Icon(look.Kind, s, t);
            };
            painter.LeftWall(along - (width / 2), v1 + 0.01, bottom, width, height, (s, t) => shade(s, t) ?? Palette.Iron);
            glow.LeftWall(along - (width / 2), v1 + 0.01, bottom, width, height, (s, t) => shade(s, t) ?? Rgba.Clear);
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
                        list.Add(new Opening(OpeningKind.Window, a, 0.7, 14, 26));
                    }
                }

                // A long strip window upstairs.
                list.Add(new Opening(OpeningKind.Window, (u0 + u1) / 2, (u1 - u0) - 0.6, 39, 50));
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
                list.Add(new Opening(OpeningKind.Window, v0 + ((v1 - v0) * 0.33), 0.6, 14, 26));
                list.Add(new Opening(OpeningKind.Window, v0 + ((v1 - v0) * 0.72), 0.6, 14, 26));
            }
            else
            {
                list.Add(new Opening(OpeningKind.Window, (v0 + v1) / 2, 0.6, 14, 26));
            }

            list.Add(new Opening(OpeningKind.Window, (v0 + v1) / 2, (v1 - v0) - 0.6, 39, 50));

            return list;
        }

        private static Rgba WallPixel(Look look, Palette palette, bool front, double along, double z, double s, List<Opening> openings, int variant, double light, Rgba neon)
        {
            foreach (var o in openings)
            {
                var inFrame = Math.Abs(along - o.Along) <= (o.Width / 2) + 0.04 && z >= o.Bottom - 2 && z <= o.Top + 2;
                if (!inFrame)
                {
                    continue;
                }

                if (!o.Contains(along, z))
                {
                    return (o.Kind == OpeningKind.Door ? neon : Palette.Iron).Shade(o.Kind == OpeningKind.Door ? 1 : light);
                }

                var local = (along - (o.Along - (o.Width / 2))) / o.Width;
                var up = (z - o.Bottom) / (o.Top - o.Bottom);
                if (o.Kind == OpeningKind.Door)
                {
                    // Sliding glass doors with a seam down the middle.
                    if (Math.Abs(local - 0.5) < 0.04)
                    {
                        return Palette.Iron;
                    }

                    return Palette.Glass.Shade(light * 1.2).Mix(Palette.GlassLight, up > 0.6 ? 0.25 : 0.05);
                }

                if (Math.Abs(up - 0.5) < 0.05 && o.Kind == OpeningKind.ShopWindow)
                {
                    return Palette.Iron;
                }

                var glass = Palette.Glass.Shade(light * 1.2);
                if (up > 0.7 && local < 0.25 && o.Width < 1)
                {
                    // A soft sheen in the top corner.
                    glass = glass.Mix(Palette.GlassLight, 0.35);
                }

                if (o.Kind == OpeningKind.ShopWindow && up < 0.3)
                {
                    var item = (int)(local * 9);
                    glass = Noise.Value(item, variant, 3) < 0.6 ? Palette.WindowGoods[item % 3].Shade(light) : glass;
                }

                return glass;
            }

            var wall = look.Wall;
            if (front && look.Neon.HasValue && z > 36 && z < 38.5 && s > 0.04 && s < 0.96)
            {
                // A neon tube along the shopfront, flush with the wall.
                return neon;
            }

            if (z < 4)
            {
                // A dark metal plinth.
                wall = Palette.Charcoal;
            }
            else if (z < 5.5)
            {
                return neon.Shade(0.8);
            }
            else if (Math.Abs(z - 34) < 0.8 && look.Kind == PlaceKind.Cottage)
            {
                // The line between floors.
                wall = wall.Shade(0.7);
            }
            else if (((along * 4) % 1) < 0.04)
            {
                // Panel seams.
                wall = wall.Shade(0.82);
            }
            else if (Noise.Value((int)(along * 8), (int)(z / 9), variant) < 0.12)
            {
                wall = wall.Shade(0.93);
            }

            if (s < 0.015 || s > 0.985)
            {
                return neon.Shade(0.9);
            }

            return wall.Shade(light);
        }

        private static Rgba GlowPixel(double along, double z, List<Opening> openings, double h, Rgba neon, int variant, bool tube = false)
        {
            foreach (var o in openings)
            {
                if (Math.Abs(along - o.Along) <= (o.Width / 2) + 0.04 && z >= o.Bottom - 2 && z <= o.Top + 2)
                {
                    if (!o.Contains(along, z))
                    {
                        // The door's frame is neon; window frames stay dark.
                        return o.Kind == OpeningKind.Door ? neon : Rgba.Clear;
                    }

                    if (o.Kind == OpeningKind.Door)
                    {
                        var local = (along - (o.Along - (o.Width / 2))) / o.Width;
                        return Math.Abs(local - 0.5) < 0.04 ? Rgba.Clear : Palette.GlassLight.WithAlpha(170);
                    }

                    // Each window is lit warm or cool.
                    var lit = Noise.Value((int)(o.Along * 10), (int)o.Bottom, variant);
                    return lit < 0.15 ? Rgba.Clear : lit < 0.6 ? Palette.WindowGlow : Palette.GlassLight;
                }
            }

            if ((z >= 4 && z < 5.5) || (tube && z > 36 && z < 38.5))
            {
                return neon;
            }

            return Rgba.Clear;
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
    }
}
