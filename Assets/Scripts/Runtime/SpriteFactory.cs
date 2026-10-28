using System.Collections.Generic;
using TinyTown.Art;
using UnityEngine;

namespace TinyTown.Runtime
{
    /// <summary>Turns pictures drawn by <c>TinyTown.Art</c> into sprites, and frees their textures when they are no longer shown.</summary>
    public sealed class SpriteFactory
    {
        public const int PixelsPerUnit = 64;

        private readonly List<Object> created = new List<Object>();

        public Sprite Create(PixelCanvas canvas, string name)
        {
            var texture = new Texture2D(canvas.Width, canvas.Height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixelData(canvas.ToBytesBottomUp(), 0);
            texture.Apply(false, true);
            var pivot = new Vector2((float)(canvas.AnchorX / canvas.Width), 1f - (float)(canvas.AnchorY / canvas.Height));
            var sprite = Sprite.Create(texture, new Rect(0, 0, canvas.Width, canvas.Height), pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            created.Add(texture);
            created.Add(sprite);
            return sprite;
        }

        /// <summary>Destroys every texture and sprite made so far.</summary>
        public void Release()
        {
            foreach (var o in created)
            {
                if (o != null)
                {
                    Object.Destroy(o);
                }
            }

            created.Clear();
        }
    }
}
