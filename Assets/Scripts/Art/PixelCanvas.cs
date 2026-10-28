using System;
using System.Collections.Generic;

namespace TinyTown.Art
{
    /// <summary>
    /// A picture being drawn. Row 0 is the top. <see cref="AnchorX"/>/<see cref="AnchorY"/> is the pixel that sits on
    /// the centre of the sprite's tile in the world, so tall things stand on their tile.
    /// </summary>
    public sealed class PixelCanvas
    {
        private readonly Rgba[] pixels;

        public PixelCanvas(int width, int height, double anchorX, double anchorY)
        {
            if (width <= 0 || height <= 0 || width > 4096 || height > 4096)
            {
                throw new ArgumentOutOfRangeException(nameof(width), $"Canvas size {width}x{height} is out of range.");
            }

            Width = width;
            Height = height;
            AnchorX = anchorX;
            AnchorY = anchorY;
            pixels = new Rgba[width * height];
        }

        public int Width { get; }

        public int Height { get; }

        public double AnchorX { get; }

        public double AnchorY { get; }

        /// <summary>The pixels, top row first.</summary>
        public IReadOnlyList<Rgba> Pixels => pixels;

        /// <summary>
        /// The pixels as RGBA bytes, bottom row first as textures expect, ready to hand to the graphics card.
        /// </summary>
        public byte[] ToBytesBottomUp()
        {
            var bytes = new byte[Width * Height * 4];
            var i = 0;
            for (var y = Height - 1; y >= 0; y--)
            {
                for (var x = 0; x < Width; x++)
                {
                    var p = pixels[(y * Width) + x];
                    bytes[i++] = p.R;
                    bytes[i++] = p.G;
                    bytes[i++] = p.B;
                    bytes[i++] = p.A;
                }
            }

            return bytes;
        }

        public Rgba Get(int x, int y) => x < 0 || y < 0 || x >= Width || y >= Height ? Rgba.Clear : pixels[(y * Width) + x];

        /// <summary>Replaces a pixel. Off-canvas pixels are ignored.</summary>
        public void Set(int x, int y, Rgba colour)
        {
            if (x >= 0 && y >= 0 && x < Width && y < Height)
            {
                pixels[(y * Width) + x] = colour;
            }
        }

        /// <summary>Paints over a pixel, blending with what is there.</summary>
        public void Paint(int x, int y, Rgba colour)
        {
            if (x >= 0 && y >= 0 && x < Width && y < Height && !colour.IsClear)
            {
                pixels[(y * Width) + x] = colour.Over(pixels[(y * Width) + x]);
            }
        }

        /// <summary>Paints beneath what is already drawn, such as a shadow added after its object.</summary>
        public void PaintUnder(int x, int y, Rgba colour)
        {
            if (x >= 0 && y >= 0 && x < Width && y < Height && !colour.IsClear)
            {
                pixels[(y * Width) + x] = pixels[(y * Width) + x].Over(colour);
            }
        }

        public void FillRect(int x, int y, int w, int h, Rgba colour)
        {
            for (var j = y; j < y + h; j++)
            {
                for (var i = x; i < x + w; i++)
                {
                    Paint(i, j, colour);
                }
            }
        }

        public void Disc(double cx, double cy, double radius, Rgba colour)
        {
            for (var y = (int)Math.Floor(cy - radius); y <= (int)Math.Ceiling(cy + radius); y++)
            {
                for (var x = (int)Math.Floor(cx - radius); x <= (int)Math.Ceiling(cx + radius); x++)
                {
                    var dx = x + 0.5 - cx;
                    var dy = y + 0.5 - cy;
                    if ((dx * dx) + (dy * dy) <= radius * radius)
                    {
                        Paint(x, y, colour);
                    }
                }
            }
        }

        /// <summary>An ellipse squashed to the isometric ground, such as a shadow or a pond.</summary>
        public void Ellipse(double cx, double cy, double rx, double ry, Rgba colour, bool under = false)
        {
            for (var y = (int)Math.Floor(cy - ry); y <= (int)Math.Ceiling(cy + ry); y++)
            {
                for (var x = (int)Math.Floor(cx - rx); x <= (int)Math.Ceiling(cx + rx); x++)
                {
                    var dx = (x + 0.5 - cx) / rx;
                    var dy = (y + 0.5 - cy) / ry;
                    if ((dx * dx) + (dy * dy) <= 1)
                    {
                        if (under)
                        {
                            PaintUnder(x, y, colour);
                        }
                        else
                        {
                            Paint(x, y, colour);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Fills a convex or concave polygon (even-odd rule), asking <paramref name="shader"/> for each pixel's colour.
        /// </summary>
        public void FillPolygon(IReadOnlyList<(double x, double y)> points, Func<int, int, Rgba> shader)
        {
            if (points.Count < 3)
            {
                return;
            }

            var minY = (int)Math.Floor(Min(points, p => p.y));
            var maxY = (int)Math.Ceiling(Max(points, p => p.y));
            var crossings = new List<double>();
            for (var y = Math.Max(0, minY); y <= Math.Min(Height - 1, maxY); y++)
            {
                var sy = y + 0.5;
                crossings.Clear();
                for (var i = 0; i < points.Count; i++)
                {
                    var a = points[i];
                    var b = points[(i + 1) % points.Count];
                    if ((a.y <= sy && b.y > sy) || (b.y <= sy && a.y > sy))
                    {
                        crossings.Add(a.x + ((sy - a.y) / (b.y - a.y) * (b.x - a.x)));
                    }
                }

                crossings.Sort();
                for (var k = 0; k + 1 < crossings.Count; k += 2)
                {
                    var from = (int)Math.Ceiling(crossings[k] - 0.5);
                    var to = (int)Math.Floor(crossings[k + 1] - 0.5);
                    for (var x = Math.Max(0, from); x <= Math.Min(Width - 1, to); x++)
                    {
                        Paint(x, y, shader(x, y));
                    }
                }
            }
        }

        /// <summary>Darkens the outside edge of everything drawn so far, for the storybook outline.</summary>
        public void Outline(Rgba colour)
        {
            var edge = new List<(int x, int y)>();
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    if (!Get(x, y).IsClear && (Get(x - 1, y).IsClear || Get(x + 1, y).IsClear || Get(x, y - 1).IsClear || Get(x, y + 1).IsClear))
                    {
                        edge.Add((x, y));
                    }
                }
            }

            foreach (var (x, y) in edge)
            {
                Set(x, y, colour.WithAlpha(Get(x, y).A).Mix(Get(x, y), 0.35));
            }
        }

        /// <summary>A copy of part of this canvas, keeping the same anchor in world terms.</summary>
        public PixelCanvas Crop(int x, int y, int w, int h)
        {
            var copy = new PixelCanvas(w, h, AnchorX - x, AnchorY - y);
            for (var j = 0; j < h; j++)
            {
                for (var i = 0; i < w; i++)
                {
                    copy.Set(i, j, Get(x + i, y + j));
                }
            }

            return copy;
        }

        public bool IsEmpty()
        {
            foreach (var p in pixels)
            {
                if (!p.IsClear)
                {
                    return false;
                }
            }

            return true;
        }

        private static double Min(IReadOnlyList<(double x, double y)> points, Func<(double x, double y), double> f)
        {
            var m = double.MaxValue;
            foreach (var p in points)
            {
                m = Math.Min(m, f(p));
            }

            return m;
        }

        private static double Max(IReadOnlyList<(double x, double y)> points, Func<(double x, double y), double> f)
        {
            var m = double.MinValue;
            foreach (var p in points)
            {
                m = Math.Max(m, f(p));
            }

            return m;
        }
    }
}
