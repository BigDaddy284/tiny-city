using System;
using System.IO;
using System.IO.Compression;

namespace TinyCity.Art
{
    /// <summary>
    /// Reads PNG files into a <see cref="PixelCanvas"/> without Unity's image module, for replacement art.
    /// Supports non-interlaced 8-bit greyscale, grey + alpha, RGB, RGBA and palette images, which is what
    /// pixel-art editors save.
    /// </summary>
    public static class PngReader
    {
        public const int MaxSize = 4096;

        private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        /// <summary>The picture in <paramref name="data"/>, or an exception explaining why it can't be read.</summary>
        public static PixelCanvas Read(byte[] data, Func<int, int, (double x, double y)> anchor)
        {
            if (data == null || data.Length < Signature.Length)
            {
                throw new InvalidDataException("Not a PNG file.");
            }

            for (var i = 0; i < Signature.Length; i++)
            {
                if (data[i] != Signature[i])
                {
                    throw new InvalidDataException("Not a PNG file.");
                }
            }

            int width = 0, height = 0, colourType = -1;
            byte[] palette = null;
            byte[] paletteAlpha = null;
            var compressed = new MemoryStream();
            var pos = Signature.Length;
            while (pos + 8 <= data.Length)
            {
                var length = ReadInt(data, pos);
                var type = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
                var start = pos + 8;
                if (length < 0 || start + length > data.Length)
                {
                    throw new InvalidDataException("The PNG file is cut short.");
                }

                switch (type)
                {
                    case "IHDR":
                        width = ReadInt(data, start);
                        height = ReadInt(data, start + 4);
                        var depth = data[start + 8];
                        colourType = data[start + 9];
                        var interlace = data[start + 12];
                        if (width <= 0 || height <= 0 || width > MaxSize || height > MaxSize)
                        {
                            throw new InvalidDataException($"The picture is {width}x{height}; at most {MaxSize}x{MaxSize} is allowed.");
                        }

                        if (depth != 8 || interlace != 0 || (colourType != 0 && colourType != 2 && colourType != 3 && colourType != 4 && colourType != 6))
                        {
                            throw new InvalidDataException("Only non-interlaced 8-bit PNG files are supported.");
                        }

                        break;
                    case "PLTE":
                        palette = new byte[length];
                        Array.Copy(data, start, palette, 0, length);
                        break;
                    case "tRNS":
                        paletteAlpha = new byte[length];
                        Array.Copy(data, start, paletteAlpha, 0, length);
                        break;
                    case "IDAT":
                        compressed.Write(data, start, length);
                        break;
                }

                if (type == "IEND")
                {
                    break;
                }

                pos = start + length + 4;
            }

            if (colourType < 0 || compressed.Length < 2)
            {
                throw new InvalidDataException("The PNG file has no picture in it.");
            }

            if (colourType == 3 && palette == null)
            {
                throw new InvalidDataException("The PNG file's palette is missing.");
            }

            var channels = colourType == 0 ? 1 : colourType == 2 ? 3 : colourType == 3 ? 1 : colourType == 4 ? 2 : 4;
            var stride = width * channels;
            var raw = Inflate(compressed.ToArray(), (stride + 1) * height);
            var (ax, ay) = anchor(width, height);
            var canvas = new PixelCanvas(width, height, ax, ay);
            var previous = new byte[stride];
            var row = new byte[stride];
            for (var y = 0; y < height; y++)
            {
                var filter = raw[y * (stride + 1)];
                Array.Copy(raw, (y * (stride + 1)) + 1, row, 0, stride);
                Unfilter(filter, row, previous, channels);
                for (var x = 0; x < width; x++)
                {
                    var i = x * channels;
                    Rgba c;
                    switch (colourType)
                    {
                        case 0: c = new Rgba(row[i], row[i], row[i]); break;
                        case 2: c = new Rgba(row[i], row[i + 1], row[i + 2]); break;
                        case 3:
                            var p = row[i] * 3;
                            if (p + 2 >= palette.Length)
                            {
                                throw new InvalidDataException("The PNG file uses a colour its palette doesn't have.");
                            }

                            var alpha = paletteAlpha != null && row[i] < paletteAlpha.Length ? paletteAlpha[row[i]] : (byte)255;
                            c = new Rgba(palette[p], palette[p + 1], palette[p + 2], alpha);
                            break;
                        case 4: c = new Rgba(row[i], row[i], row[i], row[i + 1]); break;
                        default: c = new Rgba(row[i], row[i + 1], row[i + 2], row[i + 3]); break;
                    }

                    canvas.Set(x, y, c);
                }

                var swap = previous;
                previous = row;
                row = swap;
            }

            return canvas;
        }

        private static byte[] Inflate(byte[] zlib, int expected)
        {
            // Skip the two-byte zlib header; DeflateStream reads the raw stream after it.
            var output = new byte[expected];
            using (var stream = new DeflateStream(new MemoryStream(zlib, 2, zlib.Length - 2), CompressionMode.Decompress))
            {
                var read = 0;
                while (read < expected)
                {
                    var n = stream.Read(output, read, expected - read);
                    if (n <= 0)
                    {
                        throw new InvalidDataException("The PNG file's picture data is cut short.");
                    }

                    read += n;
                }
            }

            return output;
        }

        private static void Unfilter(byte filter, byte[] row, byte[] above, int bpp)
        {
            for (var i = 0; i < row.Length; i++)
            {
                int left = i >= bpp ? row[i - bpp] : 0;
                int up = above[i];
                int corner = i >= bpp ? above[i - bpp] : 0;
                int value;
                switch (filter)
                {
                    case 0: value = row[i]; break;
                    case 1: value = row[i] + left; break;
                    case 2: value = row[i] + up; break;
                    case 3: value = row[i] + ((left + up) / 2); break;
                    case 4: value = row[i] + Paeth(left, up, corner); break;
                    default: throw new InvalidDataException($"The PNG file uses an unknown filter ({filter}).");
                }

                row[i] = (byte)value;
            }
        }

        private static int Paeth(int a, int b, int c)
        {
            var p = a + b - c;
            var pa = Math.Abs(p - a);
            var pb = Math.Abs(p - b);
            var pc = Math.Abs(p - c);
            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }

        private static int ReadInt(byte[] d, int i) => (d[i] << 24) | (d[i + 1] << 16) | (d[i + 2] << 8) | d[i + 3];
    }
}
