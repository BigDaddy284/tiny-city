using System.Collections.Generic;
using TinyCity.Art;
using TinyCity.Simulation.World;
using UnityEngine;

namespace TinyCity.Runtime
{
    /// <summary>Draws one map with the generated art, at its own place in the world so every map can be on screen at once.</summary>
    public sealed class MapView : MonoBehaviour
    {
        private readonly List<SpriteRenderer> lit = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> windows = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> blocks = new List<SpriteRenderer>();
        private readonly SpriteFactory sprites = new SpriteFactory();
        private readonly SpriteFactory blockSprites = new SpriteFactory();

        public TileMap Map { get; private set; }

        /// <summary>The art the map was last drawn with, kept so the pointer can find the building it is over.</summary>
        public MapArt Art { get; private set; }

        /// <summary>The light last applied, so it is only re-applied when it changes.</summary>
        public Daylight? CurrentLight { get; private set; }

        /// <summary>How many pictures stand on the map (not counting the ground).</summary>
        public int PieceCount => lit.Count - 1 + windows.Count;

        public void Show(TownWorld world, TileMap map, Vector3 origin, Palette palette, ArtOverrides overrides)
        {
            Clear();
            Map = map;
            transform.localPosition = origin;
            var art = MapArt.Build(world, map, palette, overrides == null ? (System.Func<ArtRequest, PixelCanvas>)null : overrides.Find);
            Art = art;

            lit.Add(Spawn("Ground", sprites.Create(art.Ground, $"{map.Id} ground"), IsoGrid.TileToWorld(0, 0), 0));
            foreach (var piece in art.Pieces)
            {
                var renderer = Spawn(piece.Name, sprites.Create(piece.Canvas, piece.Name), IsoGrid.TileToWorld(piece.TileX, piece.TileY), IsoGrid.SortingOrder(piece.SortX, piece.SortY, (int)piece.Layer));
                if (piece.Layer == ArtLayer.Glow)
                {
                    renderer.enabled = false;
                    windows.Add(renderer);
                }
                else
                {
                    lit.Add(renderer);
                }
            }
        }

        /// <summary>Replaces the Tiny City 2 building blocks drawn on this map.</summary>
        public void ShowBlocks(IReadOnlyList<ArtPiece> pieces)
        {
            ClearBlocks();
            foreach (var piece in pieces)
            {
                var renderer = Spawn(piece.Name, blockSprites.Create(piece.Canvas, piece.Name), IsoGrid.TileToWorld(piece.TileX, piece.TileY), IsoGrid.SortingOrder(piece.SortX, piece.SortY, (int)piece.Layer));
                if (CurrentLight.HasValue)
                {
                    renderer.color = ToColor(CurrentLight.Value.Tint);
                }

                blocks.Add(renderer);
            }
        }

        private void ClearBlocks()
        {
            foreach (var r in blocks)
            {
                if (r != null)
                {
                    Destroy(r.gameObject);
                }
            }

            blocks.Clear();
            blockSprites.Release();
        }

        /// <summary>Tints everything on the map with the light, and lights the windows after dark.</summary>
        public void ApplyLight(Daylight light)
        {
            if (CurrentLight.HasValue && CurrentLight.Value.Equals(light))
            {
                return;
            }

            CurrentLight = light;
            var tint = ToColor(light.Tint);
            foreach (var renderer in lit)
            {
                renderer.color = tint;
            }

            foreach (var renderer in blocks)
            {
                renderer.color = tint;
            }

            var glow = new Color(1f, 1f, 1f, (float)light.WindowGlow);
            foreach (var renderer in windows)
            {
                renderer.enabled = light.WindowGlow > 0.01;
                renderer.color = glow;
            }
        }

        public static Color ToColor(Rgba c) => new Color(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);

        public void Clear()
        {
            foreach (var r in lit)
            {
                if (r != null)
                {
                    Destroy(r.gameObject);
                }
            }

            foreach (var r in windows)
            {
                if (r != null)
                {
                    Destroy(r.gameObject);
                }
            }

            lit.Clear();
            windows.Clear();
            sprites.Release();
            ClearBlocks();
            CurrentLight = null;
            Map = null;
            Art = null;
        }

        private void OnDestroy()
        {
            sprites.Release();
            blockSprites.Release();
        }

        private SpriteRenderer Spawn(string name, Sprite sprite, Vector3 position, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }
    }
}
