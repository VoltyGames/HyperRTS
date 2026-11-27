using System;
using System.Collections.Generic;
using HyperRTS.Presentation.Environment.Vegetation.Scatter;
using HyperRTS.Presentation.Quality;
using HyperRTS.Simulation.Common.Authoring;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Presentation.Environment.Vegetation.Grass;

/// <summary>
/// Scatters a map's grass and small plants on the GPU for each camera about to render, in edit mode too, in a shadowed
/// near band and an unshadowed far band.
/// </summary>
[ExecuteAlways]
[AddComponentMenu("")]
[Icon(HyperRTSIcons.GrassRenderer)]
[HelpURL(HyperRTSDocs.Presentation)]
[DisallowMultipleComponent]
public sealed class GrassRenderer : ScatterRenderer<GrassBatch>
{
    [Tooltip("The baked plants this map grows.")]
    public GrassField field;

    private static readonly int StencilBits = Shader.PropertyToID("_StencilBits");

    // Keeps LitTeam's silhouette and ground markers off grass; RowCrop.shader writes the same bit.
    private const float GroundCoverStencil = 64f;

    private GrassComputeInputs _inputs;
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

    private protected override ScatterComputeInputs Inputs => _inputs;

    private protected override void BuildInputs()
    {
        _inputs = new GrassComputeInputs(compute, field, terrain);
        _materials = field.plants.ConvertAll(Instanced).ToArray();
    }

    // C# 14 reads a bare `field` in an accessor as the property's own backing field.
    private protected override ScatterField Field => this.field;

    private protected override List<GrassBatch> NewBatches() => new() { new GrassBatch(field, _inputs, _materials) };

    private Material Instanced(GrassPlant plant)
    {
        var material = new Material(plant.material) { name = $"{plant.name} (Grass)", hideFlags = HideFlags.DontSave };
        material.EnableKeyword("_GRASS_INSTANCES");
        material.SetFloat(StencilBits, GroundCoverStencil);
        return material;
    }

    private protected override void ReleaseInputs()
    {
        _inputs?.Dispose();
        _inputs = null;
        if (_materials != null)
        {
            Array.ForEach(_materials, Object.DestroyImmediate);
            _materials = null;
        }
    }

#if UNITY_EDITOR
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
    }
}
