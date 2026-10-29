using System;
using System.IO;
using System.Text;

namespace TinyCity.Sound
{
    /// <summary>
    /// Reads and writes plain WAV files: PCM at 8, 16 or 24 bits, or 32-bit float, in any number of channels
    /// (mixed down to mono on reading). Used for replacement sounds and for listening to the generated ones.
    /// </summary>
    public static class Wav
    {
        /// <summary>Reads a WAV file's samples and sample rate. Throws <see cref="FormatException"/> for anything it can't read.</summary>
        public static (float[] samples, int sampleRate) Read(byte[] data)
        {
            if (data == null || data.Length < 12 || Tag(data, 0) != "RIFF" || Tag(data, 8) != "WAVE")
            {
                throw new FormatException("Not a WAV file.");
            }

            int format = 0, channels = 0, rate = 0, bits = 0;
            var position = 12;
            while (position + 8 <= data.Length)
            {
                var tag = Tag(data, position);
                var size = BitConverter.ToInt32(data, position + 4);
                var body = position + 8;
                if (size < 0 || body + size > data.Length)
                {
                    // Some writers leave the data size unset; take what's there.
                    size = data.Length - body;
                }

                if (tag == "fmt " && size >= 16)
                {
                    format = BitConverter.ToUInt16(data, body);
                    channels = BitConverter.ToUInt16(data, body + 2);
                    rate = BitConverter.ToInt32(data, body + 4);
                    bits = BitConverter.ToUInt16(data, body + 14);
                    if (format == 0xFFFE && size >= 26)
                    {
                        // WAVE_FORMAT_EXTENSIBLE: the real format is the first two bytes of the sub-format GUID.
                        format = BitConverter.ToUInt16(data, body + 24);
                    }
                }
                else if (tag == "data")
                {
                    return (Decode(data, body, size, format, channels, bits), Check(rate));
                }

                position = body + size + (size % 2);
            }

            throw new FormatException("The WAV file has no sound data.");
        }

        /// <summary>Writes mono 16-bit PCM.</summary>
        public static byte[] Write(float[] samples, int sampleRate)
        {
            using (var stream = new MemoryStream())
            using (var w = new BinaryWriter(stream))
            {
                var dataSize = samples.Length * 2;
                w.Write(Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + dataSize);
                w.Write(Encoding.ASCII.GetBytes("WAVE"));
                w.Write(Encoding.ASCII.GetBytes("fmt "));
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(sampleRate);
                w.Write(sampleRate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data"));
                w.Write(dataSize);
                foreach (var s in samples)
                {
                    w.Write((short)Math.Round(Math.Max(-1f, Math.Min(1f, s)) * short.MaxValue));
                }

                w.Flush();
                return stream.ToArray();
            }
        }

        private static float[] Decode(byte[] data, int start, int size, int format, int channels, int bits)
        {
            if (channels < 1 || channels > 8)
            {
                throw new FormatException("The WAV file has an unsupported number of channels.");
            }

            var pcm = format == 1 && (bits == 8 || bits == 16 || bits == 24);
            var floats = format == 3 && bits == 32;
            if (!pcm && !floats)
            {
                throw new FormatException($"Unsupported WAV format {format} at {bits} bits; use 16-bit PCM.");
            }

            var bytes = bits / 8;
            var frames = size / (bytes * channels);
            if (frames == 0)
            {
                throw new FormatException("The WAV file has no sound data.");
            }

            var result = new float[frames];
            for (var f = 0; f < frames; f++)
            {
                var sum = 0.0;
                for (var c = 0; c < channels; c++)
                {
                    var at = start + (((f * channels) + c) * bytes);
                    sum += Sample(data, at, bits, floats);
                }

                result[f] = (float)(sum / channels);
            }

            return result;
        }

        private static double Sample(byte[] data, int at, int bits, bool floats)
        {
            if (floats)
            {
                return BitConverter.ToSingle(data, at);
            }

            switch (bits)
            {
                case 8: return (data[at] - 128) / 128.0;
                case 16: return BitConverter.ToInt16(data, at) / 32768.0;
                default: return ((data[at] << 8) | (data[at + 1] << 16) | (data[at + 2] << 24)) / 2147483648.0;
            }
        }

        private static int Check(int rate)
        {
            if (rate < 4000 || rate > 192000)
            {
                throw new FormatException("The WAV file has an unusual sample rate.");
            }

            return rate;
        }

        private static string Tag(byte[] data, int at) => at + 4 <= data.Length ? Encoding.ASCII.GetString(data, at, 4) : string.Empty;
    }
}
