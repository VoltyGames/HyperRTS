using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Vegetation.Scatter
{
    /// <summary>Base of a map's baked scatter data: the area its tile grid covers and the tiles that grow anything.</summary>
    public abstract class ScatterField : ScriptableObject
    {
        [Tooltip("World XZ rectangle the baked picture and tile grid cover. x and y are its minimum x and z.")]
        public Rect area;

        public List<ScatterTile> tiles = new();

        /// <summary>Metres the tallest plant can grow to, the height of every tile's culling box.</summary>
        public abstract float TallestPlant { get; }

        public Rect TileRect(ScatterTile tile) =>
            new(area.xMin + tile.x * ScatterTiles.Size, area.yMin + tile.z * ScatterTiles.Size,
                ScatterTiles.Size, ScatterTiles.Size);

        public ScatterTiles NewTiles(Terrain terrain) =>
            new(tiles.Count, t => TileRect(tiles[t]), TallestPlant, terrain);

        /// <summary>Problems that keep the field from drawing, one per line. Empty when ready.</summary>
        public IReadOnlyList<string> Problems()
        {
            var problems = new List<string>();
            if (area.width <= 0f || area.height <= 0f)
            {
                problems.Add($"{nameof(area)} {area} has no size");
            }

            AddProblems(problems);
            return problems;
        }

        private protected abstract void AddProblems(List<string> problems);
    }
}
