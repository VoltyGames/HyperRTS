using System;
using HyperRTS.Presentation.Environment.Ground;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Cells;
using HyperRTS.Simulation.Navigation.Areas;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Presentation.Environment.Vegetation.Scatter;

/// <summary>
/// How much each spot is a building's yard, as an R8 picture over the map: 1 inside a turned box, falling to 0
/// across <see cref="Margin"/> outside it. Published as <c>_HyperRTSFootprints</c> so grass and row crops keep
/// clear whatever the trample-map quality.
/// </summary>
public sealed partial class BuildingFootprintMask : IDisposable
{
    private static readonly MapTextureGlobal Global = new("_HyperRTSFootprints", "_HyperRTSFootprintRect");

    /// <summary>Metres from a wall over which the cover falls from 1 to 0, so plants thin out towards it.</summary>
    public const float Margin = 2f;

    // Fine enough that a turned wall's edge is not a staircase.
    public const float TexelSize = 0.5f;

    private readonly Texture2D _texture;
    private readonly byte[] _cells;
    private readonly MapSettings _map;
    private readonly CellGrid _grid;

    public BuildingFootprintMask(in MapSettings map)
    {
        _map = map;
        var nav = map.NavGrid();
        var size = (int2)math.ceil((float2)nav.Size * nav.CellSize / TexelSize);
        _grid = new CellGrid(nav.Min, TexelSize, size);
        _cells = new byte[_grid.Count];
        _texture = new Texture2D(_grid.Size.x, _grid.Size.y, TextureFormat.R8, false, true)
        {
            name = "HyperRTS Building Footprints",
            hideFlags = HideFlags.DontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
    }

    /// <summary>Bumped whenever the published mask changes, so scattered plants know to regrow.</summary>
    [AutoStaticsCleanup]
    public static int Version { get; private set; }

    public bool Fits(in MapSettings map) => map.SameNavGrid(_map);

    /// <summary>Redraws the mask from every building's turned box and publishes it.</summary>
    public void Draw(NativeArray<NavBox> boxes)
    {
        Array.Clear(_cells, 0, _cells.Length);
        foreach (var box in boxes)
        {
            Stamp(box);
        }

        _texture.SetPixelData(_cells, 0);
        _texture.Apply(false);
        Global.Publish(_texture, TerrainHeightInput.Rect(_grid.Min, (float2)_grid.Size * _grid.CellSize));
        Version++;
    }

    /// <summary>The cover stored under a point, 0 off the map.</summary>
    public float Cover(float2 position)
    {
        var cell = _grid.Cell(position);
        return _grid.Contains(cell) ? _cells[_grid.Index(cell)] / 255f : 0f;
    }

    public void Dispose()
    {
        Global.Clear();
        Version++;
        Object.DestroyImmediate(_texture);
    }

    /// <summary>Copies the published mask to a compute kernel: compute shaders don't see globals.</summary>
    public static void Bind(ComputeShader compute, int kernel) => Global.Bind(compute, kernel);

    private void Stamp(in NavBox box)
    {
        var reach = box.Extents + Margin;
        foreach (var cell in _grid.Cells(_grid.Cell(box.Center - reach), _grid.Cell(box.Center + reach)))
        {
            var cover = math.saturate(1f - box.Gap(_grid.Center(cell)) / Margin);
            var index = _grid.Index(cell);
            _cells[index] = (byte)math.max(_cells[index], (int)math.round(cover * 255f));
        }
    }
}
