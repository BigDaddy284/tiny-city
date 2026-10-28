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

            try
            {
                var canvas = PngReader.Read(File.ReadAllBytes(path), (w, h) => MapArt.AnchorFor(w, h, request.FootprintWidth, request.FootprintDepth));
                Debug.Log($"[TinyTown] Art: using {path}.");
                return canvas;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TinyTown] Art: couldn't use {path} ({e.Message}); keeping the built-in picture.");
                return null;
            }
        }
    }
}
