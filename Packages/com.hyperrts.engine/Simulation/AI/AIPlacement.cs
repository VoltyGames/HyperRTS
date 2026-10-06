using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using Unity.Mathematics;

namespace HyperRTS.Simulation.AI
{
    /// <summary>Ring search for a building spot around an AI base, keeping lanes open between buildings.</summary>
    public static class AIPlacement
    {
        /// <summary>How far the search looks, in building-plus-gap steps from home.</summary>
        public const int MaxRings = 12;

        /// <summary>
        /// Closest free spot to <paramref name="home"/>, ring by ring; candidates are a building plus
        /// <paramref name="gap"/> apart, and the gap stays clear around the spot.
        /// </summary>
        public static bool TryFindSpot(in MapSettings map, in NavGrid grid, float3 home, float2 footprint,
            PlacementSurface surface, float gap, out float3 spot)
        {
            var step = math.cmax(footprint) + gap;
            for (var ring = 1; ring <= MaxRings; ring++)
            {
                for (var k = 0; k < ring * 8; k++)
                {
                    var cell = RingCell(ring, k);
                    var position = home + new float3(cell.x, 0f, cell.y) * step;
                    if (IsFree(map, grid, position, footprint, surface, gap, out spot))
                    {
                        return true;
                    }
                }
            }

            spot = default;
            return false;
        }

        /// <summary>Cell <paramref name="k"/> (0 to 8 * ring - 1) walking the square ring's perimeter.</summary>
        private static int2 RingCell(int ring, int k)
        {
            var along = k % (2 * ring) - ring;
            return (k / (2 * ring)) switch
            {
                0 => new int2(along, -ring),
                1 => new int2(ring, along),
                2 => new int2(-along, ring),
                _ => new int2(-ring, -along),
            };
        }

        /// <summary>The single call into <see cref="PlacementMath"/>: the shared rules over a footprint padded by the gap.</summary>
        private static bool IsFree(in MapSettings map, in NavGrid grid, float3 position, float2 footprint,
            PlacementSurface surface, float gap, out float3 spot)
        {
            spot = PlacementMath.Snap(map, position, footprint);
            return PlacementMath.IsValid(map, grid, spot, footprint + 2f * gap, surface);
        }
    }
}
