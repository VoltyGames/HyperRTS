using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.AI
{
    /// <summary>Ring search for a building spot around an AI base, keeping lanes open between buildings.</summary>
    public static class AIPlacement
    {
        /// <summary>How far the search looks, in building-plus-gap steps from home.</summary>
        public const int MaxRings = 12;

        // Facing the camera, then a quarter on: further quarters repeat the same two footprints.
        private const int Facings = 2;

        /// <summary>
        /// Closest free spot to <paramref name="home"/>, ring by ring; candidates are a building plus
        /// <paramref name="gap"/> apart, the gap stays clear around the spot, and the spot passes the map's
        /// building gap to every site as the server checks it.
        /// </summary>
        public static bool TryFindSpot(in MapSettings map, in NavGrid grid, NativeArray<NavBox> sites, float3 home,
            float2 footprint, PlacementSurface surface, float gap, out BuildingSpot spot)
        {
            var step = math.cmax(footprint) + gap;
            for (var ring = 1; ring <= MaxRings; ring++)
            {
                for (var k = 0; k < ring * 8; k++)
                {
                    var cell = RingCell(ring, k);
                    var position = home + new float3(cell.x, 0f, cell.y) * step;
                    if (TryFace(map, grid, sites, new BuildingSpot(position, 0f, footprint, surface), gap, out spot))
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

        /// <summary>The first quarter yaw at which the spot, padded by the gap, is valid.</summary>
        private static bool TryFace(in MapSettings map, in NavGrid grid, NativeArray<NavBox> sites,
            in BuildingSpot candidate, float gap, out BuildingSpot spot)
        {
            for (var quarter = 0; quarter < Facings; quarter++)
            {
                var yaw = PlacementMath.CameraYaw + quarter * math.PI * 0.5f;
                spot = new BuildingSpot(candidate.Center, yaw, candidate.Footprint, candidate.Surface).Snap(map);
                if (spot.Grown(gap).IsValid(map, grid) && spot.Fits(sites, map.BuildingGap))
                {
                    return true;
                }
            }

            spot = default;
            return false;
        }
    }
}
