using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Ground.Holes
{
    /// <summary>A hole's rotated rectangle on the ground, and which texels of a <see cref="HoleGrid"/> it covers.</summary>
    public readonly struct HoleFootprint
    {
        private readonly TurnedRect _rect;

        public HoleFootprint(TurnedRect rect) => _rect = rect;

        /// <summary>The texels the rectangle's bounding box touches, clamped to the grid; empty when off it.</summary>
        public RectInt Bounds(HoleGrid grid)
        {
            var reach = _rect.Extent;
            var low = (int2)math.floor((_rect.Center - reach - grid.Origin) / grid.TexelSize);
            var high = (int2)math.ceil((_rect.Center + reach - grid.Origin) / grid.TexelSize) - 1;
            low = math.max(low, 0);
            high = math.min(high, grid.Resolution - 1);
            return new RectInt(low.x, low.y, math.max(high.x - low.x + 1, 0), math.max(high.y - low.y + 1, 0));
        }

        /// <summary>Whether the middle of the texel lies inside the rectangle.</summary>
        public bool Covers(HoleGrid grid, int x, int y) => _rect.Contains(grid.TexelCentre(x, y));

        /// <summary>Sets false, a hole, every covered texel inside <paramref name="region"/> of <paramref name="holes"/> (rows are y).</summary>
        public void Cut(HoleGrid grid, RectInt region, bool[,] holes)
        {
            var bounds = Bounds(grid);
            for (var y = Mathf.Max(bounds.yMin, region.yMin); y < Mathf.Min(bounds.yMax, region.yMax); y++)
            {
                for (var x = Mathf.Max(bounds.xMin, region.xMin); x < Mathf.Min(bounds.xMax, region.xMax); x++)
                {
                    if (Covers(grid, x, y))
                    {
                        holes[y - region.yMin, x - region.xMin] = false;
                    }
                }
            }
        }
    }
}
