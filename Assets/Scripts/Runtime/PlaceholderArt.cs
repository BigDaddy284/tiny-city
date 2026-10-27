using System.Collections.Generic;
using TinyTown.Simulation.World;
using UnityEngine;

namespace TinyTown.Runtime
{
    /// <summary>
    /// Generates simple shaded isometric shapes at startup so Phase 1 needs no art assets.
    /// Textures are greyscale and tinted per use, so one texture serves many colours.
    /// </summary>
    public static class PlaceholderArt
    {
        private const int PixelsPerUnit = 64;

        private static Sprite tile;
        private static readonly Dictionary<int, Sprite> Blocks = new Dictionary<int, Sprite>();
        private static readonly Dictionary<Color32, Sprite> People = new Dictionary<Color32, Sprite>();

        public static Sprite Tile => tile != null ? tile : (tile = MakeTile());

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

        /// <summary>An isometric box whose top sits <paramref name="heightPixels"/> above its base tile.</summary>
        public static Sprite Block(int heightPixels)
        {
            if (!Blocks.TryGetValue(heightPixels, out var sprite) || sprite == null)
            {
                sprite = MakeBlock(heightPixels);
                Blocks[heightPixels] = sprite;
            }

            return sprite;
        }

        public static Color GroundColour(TileType type, int x, int y)
        {
            var checker = ((x + y) & 1) == 0 ? 1f : 0.94f;
            Color c;
            switch (type)
            {
                case TileType.Grass: c = new Color(0.55f, 0.74f, 0.42f); break;
                case TileType.Path: c = new Color(0.86f, 0.77f, 0.58f); break;
                case TileType.Floor: c = new Color(0.78f, 0.6f, 0.42f); break;
                case TileType.Garden: c = new Color(0.52f, 0.37f, 0.24f); break;
                case TileType.Door: c = new Color(0.55f, 0.33f, 0.2f); break;
                case TileType.Water: c = new Color(0.45f, 0.67f, 0.86f); break;
                default: c = new Color(0.5f, 0.5f, 0.5f); break;
            }

            return new Color(c.r * checker, c.g * checker, c.b * checker);
        }

        /// <summary>Colour and height for tiles that stand up off the ground, or false for flat tiles.</summary>
        public static bool TryGetBlock(TileType type, out Color colour, out int heightPixels)
        {
            switch (type)
            {
                case TileType.Tree:
                    colour = new Color(0.3f, 0.52f, 0.3f);
                    heightPixels = 30;
                    return true;
                case TileType.House:
                    colour = new Color(0.86f, 0.55f, 0.42f);
                    heightPixels = 34;
                    return true;
                case TileType.Wall:
                    colour = new Color(0.93f, 0.86f, 0.74f);
                    heightPixels = 10;
                    return true;
                default:
                    colour = Color.white;
                    heightPixels = 0;
                    return false;
            }
        }

        public static Color ObjectColour(ObjectType type)
        {
            switch (type)
            {
                case ObjectType.Bed: return new Color(0.45f, 0.58f, 0.86f);
                case ObjectType.Kitchen: return new Color(0.9f, 0.9f, 0.88f);
                case ObjectType.Sink: return new Color(0.7f, 0.86f, 0.95f);
                case ObjectType.Toilet: return new Color(0.97f, 0.97f, 0.97f);
                case ObjectType.Shower: return new Color(0.55f, 0.85f, 0.85f);
                case ObjectType.Armchair: return new Color(0.62f, 0.45f, 0.7f);
                case ObjectType.GardenPlot: return new Color(0.4f, 0.75f, 0.3f);
                case ObjectType.GardenTap: return new Color(0.6f, 0.62f, 0.66f);
                case ObjectType.Bench: return new Color(0.58f, 0.4f, 0.26f);
                case ObjectType.ExerciseSpot: return new Color(0.95f, 0.6f, 0.25f);
                case ObjectType.Workbench: return new Color(0.72f, 0.52f, 0.3f);
                case ObjectType.ShopShelf: return new Color(0.85f, 0.45f, 0.35f);
                case ObjectType.Counter: return new Color(0.5f, 0.32f, 0.2f);
                case ObjectType.CafeTable: return new Color(0.95f, 0.85f, 0.6f);
                case ObjectType.BakeryDisplay: return new Color(0.98f, 0.75f, 0.5f);
                case ObjectType.ClinicBed: return new Color(0.85f, 0.92f, 0.98f);
                case ObjectType.Easel: return new Color(0.9f, 0.55f, 0.75f);
                case ObjectType.Piano: return new Color(0.2f, 0.18f, 0.2f);
                case ObjectType.ChessTable: return new Color(0.95f, 0.95f, 0.9f);
                case ObjectType.FishingSpot: return new Color(0.3f, 0.5f, 0.75f);
                default: return Color.magenta;
            }
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

        private static Sprite MakeTile()
        {
            const int w = 64;
            const int h = 32;
            var pixels = new Color32[w * h];
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var d = (Mathf.Abs(x + 0.5f - (w / 2f)) / (w / 2f)) + (Mathf.Abs(y + 0.5f - (h / 2f)) / (h / 2f));
                    byte shade = d <= 0.93f ? (byte)255 : (byte)225;
                    pixels[(y * w) + x] = d <= 1f ? new Color32(shade, shade, shade, 255) : new Color32(0, 0, 0, 0);
                }
            }

            return Create(pixels, w, h, new Vector2(0.5f, 0.5f));
        }

        private static Sprite MakeBlock(int height)
        {
            const int w = 64;
            const int baseH = 32;
            var h = baseH + height;
            var pixels = new Color32[w * h];
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var fx = x + 0.5f;
                    var fy = y + 0.5f;
                    var top = (Mathf.Abs(fx - 32f) / 32f) + (Mathf.Abs(fy - (height + 16f)) / 16f) <= 1f;
                    byte shade = 0;
                    if (top)
                    {
                        shade = 255;
                    }
                    else if (fx < 32f && fy >= 16f - (fx / 2f) && fy < height + 16f - (fx / 2f))
                    {
                        shade = 170;
                    }
                    else if (fx >= 32f && fy >= (fx - 32f) / 2f && fy < ((fx - 32f) / 2f) + height)
                    {
                        shade = 210;
                    }

                    pixels[(y * w) + x] = shade == 0 ? new Color32(0, 0, 0, 0) : new Color32(shade, shade, shade, 255);
                }
            }

            return Create(pixels, w, h, new Vector2(0.5f, 16f / h));
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
