using System.IO;
using System.IO.Compression;
using TinyCity.Art;

namespace TinyCity.Tools.ArtPreview
{
    /// <summary>A minimal PNG writer (8-bit RGBA, no filtering) so the preview needs no image library.</summary>
    internal static class Png
    {
        public static void Write(string path, PixelCanvas canvas, int scale = 1)
        {
            var w = canvas.Width * scale;
            var h = canvas.Height * scale;
            var raw = new MemoryStream();
            for (var y = 0; y < h; y++)
            {
                raw.WriteByte(0);
                for (var x = 0; x < w; x++)
                {
                    var p = canvas.Get(x / scale, y / scale);
                    raw.WriteByte(p.R);
                    raw.WriteByte(p.G);
                    raw.WriteByte(p.B);
                    raw.WriteByte(p.A);
                }
            }

            var compressed = new MemoryStream();
            using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, true))
            {
                raw.Position = 0;
                raw.CopyTo(z);
            }

            using var file = File.Create(path);
            file.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            var header = new MemoryStream();
            WriteInt(header, w);
            WriteInt(header, h);
            header.Write(new byte[] { 8, 6, 0, 0, 0 });
            Chunk(file, "IHDR", header.ToArray());
            Chunk(file, "IDAT", compressed.ToArray());
            Chunk(file, "IEND", new byte[0]);
        }

        private static void Chunk(Stream s, string type, byte[] data)
        {
            WriteInt(s, data.Length);
            var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            s.Write(typeBytes);
            s.Write(data);
            var crc = Crc(typeBytes, data);
            WriteInt(s, (int)crc);
        }

        private static void WriteInt(Stream s, int v)
        {
            s.WriteByte((byte)(v >> 24));
            s.WriteByte((byte)(v >> 16));
            s.WriteByte((byte)(v >> 8));
            s.WriteByte((byte)v);
        }

        private static uint Crc(byte[] a, byte[] b)
        {
            var c = 0xffffffffu;
            foreach (var part in new[] { a, b })
            {
                foreach (var x in part)
                {
                    c ^= x;
                    for (var k = 0; k < 8; k++)
                    {
                        c = (c & 1) != 0 ? 0xedb88320u ^ (c >> 1) : c >> 1;
                    }
                }
            }

            return c ^ 0xffffffffu;
        }
    }
}
