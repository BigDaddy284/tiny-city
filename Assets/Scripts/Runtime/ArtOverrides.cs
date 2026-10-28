using System;
using System.Collections.Generic;
using System.IO;
using TinyTown.Art;
using UnityEngine;

namespace TinyTown.Runtime
{
    /// <summary>
    /// Pictures the player has put in <c>StreamingAssets/Art</c> to replace generated ones, named by
    /// <see cref="ArtRequest.Key"/> (for example <c>tree.png</c>, <c>tree-winter.png</c>, <c>object-bed.png</c> or
    /// <c>building-map-cafe.png</c>). See <c>Documentation/ArtOverrides.md</c>. With no folder, nothing is replaced.
    /// </summary>
    public sealed class ArtOverrides
    {
        private readonly string folder;
        private readonly Dictionary<string, PixelCanvas> cache = new Dictionary<string, PixelCanvas>();
        private readonly bool any;

        public ArtOverrides(string folder)
        {
            this.folder = folder;
            any = !string.IsNullOrEmpty(folder) && Directory.Exists(folder);
        }

        public static ArtOverrides FromStreamingAssets() => new ArtOverrides(Path.Combine(Application.streamingAssetsPath, "Art"));

        /// <summary>The replacement for a request, or null to keep the generated picture.</summary>
        public PixelCanvas Find(ArtRequest request)
        {
            if (!any)
            {
                return null;
            }

            if (!cache.TryGetValue(request.Key, out var canvas))
            {
                canvas = Load(request);
                cache[request.Key] = canvas;
            }

            return canvas;
        }

        private PixelCanvas Load(ArtRequest request)
        {
            var path = Path.Combine(folder, request.Key + ".png");
            if (!File.Exists(path))
            {
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path)) || texture.width > 4096 || texture.height > 4096)
                {
                    Debug.LogWarning($"[TinyTown] Art: couldn't use {path}; keeping the built-in picture.");
                    return null;
                }

                var pixels = texture.GetPixels32();
                var bytes = new byte[pixels.Length * 4];
                for (var i = 0; i < pixels.Length; i++)
                {
                    bytes[(i * 4) + 0] = pixels[i].r;
                    bytes[(i * 4) + 1] = pixels[i].g;
                    bytes[(i * 4) + 2] = pixels[i].b;
                    bytes[(i * 4) + 3] = pixels[i].a;
                }

                var (ax, ay) = MapArt.AnchorFor(texture.width, texture.height, request.FootprintWidth, request.FootprintDepth);
                Debug.Log($"[TinyTown] Art: using {path}.");
                return PixelCanvas.FromBytesBottomUp(bytes, texture.width, texture.height, ax, ay);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TinyTown] Art: couldn't read {path}: {e.Message}");
                return null;
            }
            finally
            {
                UnityEngine.Object.Destroy(texture);
            }
        }
    }
}
