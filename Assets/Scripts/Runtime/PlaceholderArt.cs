using UnityEngine;

namespace TinyCity.Runtime
{
    /// <summary>
    /// Plain one-colour textures for IMGUI backgrounds. Everything in the town itself is drawn by <c>TinyCity.Art</c>:
    /// places through <see cref="MapView"/> and residents through <see cref="TownView"/>.
    /// </summary>
    public static class PlaceholderArt
    {
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
    }
}
