using System.Collections.Generic;
using TinyTown.Simulation.World;
using UnityEngine;

namespace TinyTown.Runtime
{
    /// <summary>
    /// The remaining placeholder pictures: the residents' figures (replaced in Phase 6, build 2) and plain
    /// one-colour textures for IMGUI backgrounds. Places are drawn by <c>TinyTown.Art</c> through <see cref="MapView"/>.
    /// </summary>
    public static class PlaceholderArt
    {
        private const int PixelsPerUnit = 64;

        private static readonly Dictionary<Color32, Sprite> People = new Dictionary<Color32, Sprite>();

        /// <summary>A little figure with clothes in <paramref name="clothes"/>. Not tinted, so the face keeps its colour.</summary>
        public static Sprite Person(Color clothes)
        {
            Color32 key = clothes;
            if (!People.TryGetValue(key, out var sprite) || sprite == null)
            {
                sprite = MakePerson(key);
                People[key] = sprite;
            }

            return sprite;
        }

        /// <summary>A one-pixel texture of a single colour, for IMGUI backgrounds such as speech bubbles.</summary>
        public static Texture2D Solid(Color colour)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, colour);
            texture.Apply();
            return texture;
        }

        public static Color ParseColour(string hex, Color fallback)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;
        }

        private static Sprite MakePerson(Color32 clothes)
        {
            const int w = 20;
            const int h = 36;
            var pixels = new Color32[w * h];
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var fx = x + 0.5f;
                    var fy = y + 0.5f;
                    var head = ((fx - 10f) * (fx - 10f)) + ((fy - 28f) * (fy - 28f)) <= 49f;
                    var body = Mathf.Abs(fx - 10f) <= 8f - (fy * 0.06f) && fy <= 22f && fy >= 2f;
                    var outline = !head && !body && (((fx - 10f) * (fx - 10f)) + ((fy - 28f) * (fy - 28f)) <= 64f || (Mathf.Abs(fx - 10f) <= 9f && fy <= 23f && fy >= 1f));
                    pixels[(y * w) + x] = head ? new Color32(255, 236, 214, 255)
                        : body ? clothes
                        : outline ? new Color32(60, 40, 30, 255)
                        : new Color32(0, 0, 0, 0);
                }
            }

            return Create(pixels, w, h, new Vector2(0.5f, 0f));
        }

        private static Sprite Create(Color32[] pixels, int w, int h, Vector2 pivot)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, w, h), pivot, PixelsPerUnit);
        }
    }
}
