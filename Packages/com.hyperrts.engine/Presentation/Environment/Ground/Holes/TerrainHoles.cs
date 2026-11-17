using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Ground.Holes
{
    /// <summary>
    /// A Unity Terrain with building holes cut in it, and its holes and clipping from before the first cut to put
    /// back. Visual only: the simulation's ground never reads it.
    /// </summary>
    public sealed class TerrainHoles : IDisposable
    {
        // The terrain shader clips holes only in this variant, which maps leave off to spare the alpha test.
        private const string ClipKeyword = "_ALPHATEST_ON";

        private readonly TerrainData _data;
        private readonly Material _material;
        private readonly bool _clipped;
        private readonly bool[,] _original;
        private readonly HoleGrid _grid;
        private readonly Dictionary<Entity, HoleFootprint> _cut = new();
        private readonly List<Entity> _gone = new();
        private bool _changed;

        public TerrainHoles(Terrain terrain)
        {
            Terrain = terrain;
            _data = terrain.terrainData;
            _material = terrain.materialTemplate;
            _clipped = _material != null && _material.IsKeywordEnabled(ClipKeyword);
            var resolution = _data.holesResolution;
            _original = _data.GetHoles(0, 0, resolution, resolution);
            var position = terrain.GetPosition();
            _grid = new HoleGrid(new float2(position.x, position.z), new float2(_data.size.x, _data.size.z), resolution);
        }

        public Terrain Terrain { get; }

        /// <summary>Cuts what <paramref name="wanted"/> adds and fills what it drops, in one write over their bounds.</summary>
        public void Apply(Dictionary<Entity, HoleFootprint> wanted)
        {
            var region = new HoleRegion();
            _gone.Clear();
            foreach (var (entity, footprint) in _cut)
            {
                if (!wanted.ContainsKey(entity))
                {
                    _gone.Add(entity);
                    region.Add(footprint.Bounds(_grid));
                }
            }

            foreach (var entity in _gone)
            {
                _cut.Remove(entity);
            }

            foreach (var (entity, footprint) in wanted)
            {
                if (_cut.TryAdd(entity, footprint))
                {
                    region.Add(footprint.Bounds(_grid));
                }
            }

            if (!region.IsEmpty)
            {
                Write(region.Rect);
            }
        }

        /// <summary>Puts the holes from before the first cut back, so none are saved or outlive a map.</summary>
        public void Dispose()
        {
            // A terrain unloaded with its scene may have taken its TerrainData with it.
            if (_data != null && _changed)
            {
                _data.SetHoles(0, 0, _original);
            }

            if (_material != null && _changed && !_clipped)
            {
                _material.DisableKeyword(ClipKeyword);
            }

            _cut.Clear();
            _changed = false;
        }

        private void Write(RectInt rect)
        {
            var holes = new bool[rect.height, rect.width];
            for (var y = 0; y < rect.height; y++)
            {
                for (var x = 0; x < rect.width; x++)
                {
                    holes[y, x] = _original[rect.y + y, rect.x + x];
                }
            }

            foreach (var footprint in _cut.Values)
            {
                footprint.Cut(_grid, rect, holes);
            }

            _data.SetHoles(rect.x, rect.y, holes);
            if (_material != null)
            {
                _material.EnableKeyword(ClipKeyword);
            }

            _changed = true;
        }
    }
}
