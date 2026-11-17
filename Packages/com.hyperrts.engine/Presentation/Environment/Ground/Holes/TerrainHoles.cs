using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

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
        private readonly Material _template;
        private Material _clipping;
        private readonly bool[,] _original;
        private readonly HoleGrid _grid;
        private readonly Dictionary<Entity, HoleFootprint> _cut = new();
        private readonly List<Entity> _gone = new();
        private bool _changed;

        public TerrainHoles(Terrain terrain)
        {
            Terrain = terrain;
            _data = terrain.terrainData;
            _template = terrain.materialTemplate;
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
                if (_cut.TryGetValue(entity, out var last))
                {
                    if (last.Equals(footprint))
                    {
                        continue;
                    }

                    region.Add(last.Bounds(_grid));
                }

                _cut[entity] = footprint;
                region.Add(footprint.Bounds(_grid));
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

            if (_clipping != null)
            {
                // A terrain unloaded with its scene is gone already.
                if (Terrain != null)
                {
                    Terrain.materialTemplate = _template;
                }

                CoreUtils.Destroy(_clipping);
            }

            _clipping = null;

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
            EnableClipping();
            _changed = true;
        }

        // The template is a shared asset, so the keyword goes on a copy this terrain draws with.
        private void EnableClipping()
        {
            if (_clipping != null || _template == null || _template.IsKeywordEnabled(ClipKeyword))
            {
                return;
            }

            _clipping = new Material(_template) { name = $"{_template.name} (Holes)", hideFlags = HideFlags.DontSave };
            _clipping.EnableKeyword(ClipKeyword);
            Terrain.materialTemplate = _clipping;
        }
    }
}
