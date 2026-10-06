using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Building placement rules shared by the ghost preview, the authoritative placement command and the AI.</summary>
    public static class PlacementMath
    {
        /// <summary>Snaps a footprint centre so its edges land on nav cell boundaries.</summary>
        public static float3 Snap(in MapSettings map, float3 position, float2 footprint)
        {
            var cells = math.max(1f, math.round(footprint / map.NavCellSize));
            var offset = math.select(float2.zero, new float2(0.5f), math.fmod(cells, 2f) > 0.5f);
            var local = (position.xz - map.Min) / map.NavCellSize - offset;
            var snapped = (math.round(local) + offset) * map.NavCellSize + map.Min;
            return new float3(snapped.x, position.y, snapped.y);
        }

        /// <summary>
        /// Inside the map and, when a grid exists, over free cells of the surface only: plain land or plain water,
        /// never a deck or a blocked cell. Without a grid only land placement is possible.
        /// </summary>
        public static bool IsValid(in MapSettings map, in NavGrid grid, float3 center, float2 footprint,
            PlacementSurface surface = PlacementSurface.Land)
        {
            var half = new float3(footprint.x * 0.5f, 0f, footprint.y * 0.5f);
            if (!map.Contains(center - half) || !map.Contains(center + half))
            {
                return false;
            }

            if (!grid.IsCreated)
            {
                return surface == PlacementSurface.Land;
            }

            return Covers(grid, center, footprint, surface);
        }

        /// <summary>Snaps and grounds <paramref name="position"/> into <paramref name="center"/>, which is set even when the spot is invalid.</summary>
        public static bool Resolve(in MapSettings map, in NavGrid grid, float3 position, float2 footprint,
            PlacementSurface surface, out float3 center)
        {
            center = Snap(map, position, footprint);
            center.y = Height(grid, center);
            return IsValid(map, grid, center, footprint, surface);
        }

        /// <summary>Height a building stands at: the water surface over water cells, the ground elsewhere.</summary>
        public static float Height(in NavGrid grid, float3 center) =>
            grid.IsCreated ? grid.SurfaceHeight(center) : center.y;

        private static bool Covers(in NavGrid grid, float3 center, float2 footprint, PlacementSurface surface)
        {
            grid.GetArea(center, footprint, out var min, out var max);
            var land = 0;
            var water = 0;
            for (var y = min.y; y <= max.y; y++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    var cell = grid.Surface(new int2(x, y));
                    if (cell == NavSurface.Land)
                    {
                        land++;
                    }
                    else if (cell == NavSurface.Water)
                    {
                        water++;
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            switch (surface)
            {
                case PlacementSurface.Water:
                    return land == 0;
                case PlacementSurface.Shoreline:
                    return land > 0 && water > 0;
                default:
                    return water == 0;
            }
        }
    }
}
