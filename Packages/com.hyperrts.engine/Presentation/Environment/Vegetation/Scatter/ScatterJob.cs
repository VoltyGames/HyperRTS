using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment.Vegetation.Scatter;

/// <summary>One visible tile of one grass plant or crop field for a scatter kernel to grow (Scatter.hlsl includes the generated layout).</summary>
[GenerateHLSL(needAccessors: false)]
public struct ScatterJob(Rect tile, int id, Vector2Int first, Vector2Int count)
{
    /// <summary>World XZ of the tile's minimum corner.</summary>
    public Vector2 tileMin = tile.min;

    /// <summary>Index into the renderer's plants or fields.</summary>
    public int id = id;

    /// <summary>The lattice spots whose plant can fall in the tile, and how many of each axis.</summary>
    public Vector2Int first = first;
    public Vector2Int count = count;

    /// <summary>Kernel threads the job needs: one per spot.</summary>
    public int Threads => count.x * count.y;

    /// <summary>A job over the world-anchored square lattice of <paramref name="spacing"/> metres.</summary>
    public static ScatterJob Square(Rect tile, int id, float spacing)
    {
        var first = new Vector2Int(Mathf.FloorToInt(tile.xMin / spacing), Mathf.FloorToInt(tile.yMin / spacing));
        var last = new Vector2Int(Mathf.FloorToInt(tile.xMax / spacing), Mathf.FloorToInt(tile.yMax / spacing));
        return new ScatterJob(tile, id, first, last - first + Vector2Int.one);
    }
}
