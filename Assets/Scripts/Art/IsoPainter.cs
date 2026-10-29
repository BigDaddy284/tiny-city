using System;

namespace TinyCity.Art
{
    /// <summary>
    /// Draws 3D shapes onto a <see cref="PixelCanvas"/> in the game's isometric projection. Positions are in tiles
    /// (<c>u</c> along a map's x, <c>v</c> along its y, the canvas's anchor tile covering 0..1 in both) and height
    /// <c>z</c> is in pixels. A tile is 64 x 32 pixels, the same as one world unit at 64 pixels per unit.
    /// </summary>
    public sealed class IsoPainter
    {
        public const int HalfTileWidth = 32;
        public const int HalfTileHeight = 16;

        /// <summary>Brightness of the faces of a box: lit from the upper left, as in the rest of the game.</summary>
        public const double TopLight = 1.0;
        public const double LeftLight = 0.84;
        public const double RightLight = 0.68;

        public IsoPainter(PixelCanvas canvas)
        {
            Canvas = canvas;
        }

        public PixelCanvas Canvas { get; }

        /// <summary>Where a point lands on the canvas, in pixels from its top left.</summary>
        public (double x, double y) Project(double u, double v, double z)
        {
            return (Canvas.AnchorX + ((u - v) * HalfTileWidth), Canvas.AnchorY + ((u + v - 1) * HalfTileHeight) - z);
        }

        /// <summary>
        /// Fills the parallelogram <paramref name="origin"/> + s * <paramref name="e1"/> + t * <paramref name="e2"/>
        /// (s and t from 0 to 1), asking <paramref name="shader"/> for the colour at each pixel's (s, t).
        /// The shader may return <see cref="Rgba.Clear"/> to leave a pixel alone, which makes triangles and holes.
        /// </summary>
        public void Face((double u, double v, double z) origin, (double u, double v, double z) e1, (double u, double v, double z) e2, Func<double, double, Rgba> shader)
        {
            var p0 = Project(origin.u, origin.v, origin.z);
            var a = Delta(e1);
            var b = Delta(e2);
            var det = (a.x * b.y) - (a.y * b.x);
            if (Math.Abs(det) < 1e-6)
            {
                return;
            }

            var corners = new[]
            {
                p0,
                (p0.x + a.x, p0.y + a.y),
                (p0.x + a.x + b.x, p0.y + a.y + b.y),
                (p0.x + b.x, p0.y + b.y),
            };
            Canvas.FillPolygon(corners, (px, py) =>
            {
                var dx = px + 0.5 - p0.x;
                var dy = py + 0.5 - p0.y;
                var s = ((dx * b.y) - (dy * b.x)) / det;
                var t = ((a.x * dy) - (a.y * dx)) / det;
                return shader(Clamp01(s), Clamp01(t));
            });
        }

        /// <summary>A flat-coloured box from (u, v, z) of size (du, dv, dz), showing its top and two front faces.</summary>
        public void Box(double u, double v, double z, double du, double dv, double dz, Rgba colour)
        {
            Box(u, v, z, du, dv, dz, colour.Shade(TopLight), colour.Shade(LeftLight), colour.Shade(RightLight));
        }

        public void Box(double u, double v, double z, double du, double dv, double dz, Rgba top, Rgba left, Rgba right)
        {
            if (dz > 0)
            {
                Face((u, v + dv, z), (du, 0, 0), (0, 0, dz), (s, t) => left);
                Face((u + du, v, z), (0, dv, 0), (0, 0, dz), (s, t) => right);
            }

            Top(u, v, z + dz, du, dv, (s, t) => top);
        }

        /// <summary>A flat rectangle at height <paramref name="z"/>, shaded by (s along u, t along v).</summary>
        public void Top(double u, double v, double z, double du, double dv, Func<double, double, Rgba> shader)
        {
            Face((u, v, z), (du, 0, 0), (0, dv, 0), shader);
        }

        /// <summary>The front-left wall of a box (facing +v), shaded by (s along u, t up).</summary>
        public void LeftWall(double u, double v, double z, double du, double dz, Func<double, double, Rgba> shader)
        {
            Face((u, v, z), (du, 0, 0), (0, 0, dz), shader);
        }

        /// <summary>The front-right wall of a box (facing +u), shaded by (s along v, t up).</summary>
        public void RightWall(double u, double v, double z, double dv, double dz, Func<double, double, Rgba> shader)
        {
            Face((u, v, z), (0, dv, 0), (0, 0, dz), shader);
        }

        /// <summary>An upright cylinder, such as a table leg or a post.</summary>
        public void Post(double u, double v, double z, double radius, double height, Rgba colour)
        {
            var (cx, cy) = Project(u, v, z);
            var rx = radius * HalfTileWidth * 1.41;
            var ry = rx / 2;
            for (var y = (int)Math.Floor(cy - height - ry); y <= (int)Math.Ceiling(cy + ry); y++)
            {
                for (var x = (int)Math.Floor(cx - rx); x <= (int)Math.Ceiling(cx + rx); x++)
                {
                    var dx = (x + 0.5 - cx) / rx;
                    if (Math.Abs(dx) > 1)
                    {
                        continue;
                    }

                    var curve = Math.Sqrt(1 - (dx * dx)) * ry;
                    var py = y + 0.5;
                    if (py <= cy + curve && py >= cy - height - curve)
                    {
                        var light = py < cy - height + curve ? TopLight : 0.95 - (0.3 * (dx + 1) / 2);
                        Canvas.Paint(x, y, colour.Shade(light));
                    }
                }
            }
        }

        /// <summary>A flat disc lying at height <paramref name="z"/>, such as a round table top.</summary>
        public void FlatDisc(double u, double v, double z, double radius, Rgba colour)
        {
            var (cx, cy) = Project(u, v, z);
            var rx = radius * HalfTileWidth * 1.41;
            Canvas.Ellipse(cx, cy, rx, rx / 2, colour);
        }

        private static (double x, double y) Delta((double u, double v, double z) e)
        {
            return ((e.u - e.v) * HalfTileWidth, ((e.u + e.v) * HalfTileHeight) - e.z);
        }

        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
