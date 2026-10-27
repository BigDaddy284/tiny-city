using System.Collections.Generic;
using TinyTown.Simulation.World;
using UnityEngine;

namespace TinyTown.Runtime
{
    /// <summary>Draws one map with placeholder sprites. Rebuilt whenever the viewed map changes.</summary>
    public sealed class MapView : MonoBehaviour
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        public TileMap Map { get; private set; }

        public void Show(TownWorld world, TileMap map)
        {
            Clear();
            Map = map;
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    var type = map.GetTile(new GridPos(x, y));
                    if (PlaceholderArt.TryGetBlock(type, out var colour, out var height))
                    {
                        Spawn($"{type} {x},{y}", PlaceholderArt.Block(height), colour, IsoGrid.TileToWorld(x, y), IsoGrid.SortingOrder(x, y, 0));
                    }
                    else
                    {
                        Spawn($"{type} {x},{y}", PlaceholderArt.Tile, PlaceholderArt.GroundColour(type, x, y), IsoGrid.TileToWorld(x, y), 0);
                    }
                }
            }

            foreach (var obj in world.Objects)
            {
                if (obj.Location.MapId != map.Id)
                {
                    continue;
                }

                var p = obj.Location.Position;
                var go = Spawn(obj.Name, PlaceholderArt.Block(12), PlaceholderArt.ObjectColour(obj.Type), IsoGrid.TileToWorld(p.X, p.Y), IsoGrid.SortingOrder(p.X, p.Y, 1));
                go.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
            }
        }

        public void Clear()
        {
            foreach (var go in spawned)
            {
                Destroy(go);
            }

            spawned.Clear();
            Map = null;
        }

        private GameObject Spawn(string name, Sprite sprite, Color colour, Vector3 position, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = colour;
            renderer.sortingOrder = order;
            spawned.Add(go);
            return go;
        }
    }
}
