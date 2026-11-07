using System;
using System.Collections.Generic;
using HyperRTS.Presentation.Environment.Vegetation.Scatter;
using HyperRTS.Presentation.Quality;
using HyperRTS.Simulation.Common.Authoring;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Presentation.Environment.Vegetation.Grass
{
    /// <summary>
    /// Scatters a map's grass and small plants on the GPU from a <see cref="GrassField"/> for each camera about to
    /// render, in edit mode too, and draws them in a shadowed near band and an unshadowed far band. Each camera keeps
    /// its own buffers and rescatters only when its view or the quality settings change.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("")]
    [Icon(HyperRTSIcons.Environment)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public sealed class GrassRenderer : ScatterRenderer<GrassBatch>
    {
        [Tooltip("The baked plants this map grows.")]
        public GrassField field;

        [Tooltip("The Hidden/HyperRTS/FoliageGrass shader each plant's material is copied onto.")]
        public Shader shader;

        private GrassComputeInputs _kernels;
        private Material[] _materials;

        private protected override ScatterSettings Settings
        {
            get
            {
                var quality = PresentationQuality.Current;
                return new ScatterSettings(quality.GrassDensity, quality.GrassDistance, quality.GrassShadowDistance,
                    quality.CloseDetail);
            }
        }

        private protected override ScatterComputeInputs Inputs => _kernels;

        private protected override void BuildInputs()
        {
            _kernels = new GrassComputeInputs(compute, field, terrain);
            _materials = field.plants.ConvertAll(Instanced).ToArray();
        }

        private protected override ScatterField Field => field;

        private protected override List<GrassBatch> NewBatches() => new() { new GrassBatch(field, _kernels, _materials) };

        private Material Instanced(GrassPlant plant)
        {
            var material = new Material(shader) { name = $"{plant.name} (Grass)", hideFlags = HideFlags.DontSave };
            material.CopyPropertiesFromMaterial(plant.material);
            return material;
        }

        private protected override void ReleaseInputs()
        {
            _kernels?.Dispose();
            _kernels = null;
            if (_materials != null)
            {
                Array.ForEach(_materials, Object.DestroyImmediate);
                _materials = null;
            }
        }

#if UNITY_EDITOR
        private protected override bool ReferencesStale() => shader == null;

        private protected override bool Stale(List<GrassBatch> batches) => batches[0].Stale();
#endif

        private protected override void Queue(List<GrassBatch> batches, Vector3 position, ScatterSettings settings)
        {
            var batch = batches[0];
            foreach (var t in Tiles.Visible)
            {
                var bounds = Tiles[t];
                var range = ScatterTiles.Distances(bounds, position);
                var tile = field.tiles[t];
                var rect = field.TileRect(tile);
                for (var i = tile.first; i < tile.first + tile.count; i++)
                {
                    var grows = field.tilePlants[i];
                    var plant = field.plants[grows.plant];
                    if (range.Near >= plant.DistanceAt(settings.Distance) || plant.ShareAt(settings.CloseDetail) == 0f)
                    {
                        continue;
                    }

                    var job = ScatterJob.Square(rect, grows.plant, plant.Spacing);
                    batch.Add(job, grows.coverage, bounds, range, settings);
                }
            }
        }

        private protected override void AddProblems(List<string> problems)
        {
            if (field == null)
            {
                problems.Add($"{nameof(field)} is missing");
            }
            else
            {
                problems.AddRange(field.Problems());
            }

            if (shader == null)
            {
                problems.Add($"{nameof(shader)} is missing");
            }
        }
    }
}
