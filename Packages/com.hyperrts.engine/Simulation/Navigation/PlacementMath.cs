using HyperRTS.Simulation.Common;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Building placement rules shared by the ghost preview, the authoritative placement command and the AI.</summary>
    public static class PlacementMath
    {
        /// <summary>Quarter turns a placed building can take, so turn values run 0 to 3.</summary>
        public const int Turns = 4;

        /// <summary>
        /// Yaw of a building placed at <paramref name="turn"/>. Turn 0 points its front (+Z) at the match camera,
        /// which looks along +Z; each turn adds a quarter clockwise seen from above.
        /// </summary>
        public static quaternion Facing(int turn) => quaternion.RotateY(math.PI * (1f + 0.5f * turn));

        /// <summary>The footprint's world X and Z at <paramref name="turn"/>: odd turns swap them.</summary>
        public static float2 Turned(float2 footprint, int turn) => turn % 2 == 1 ? footprint.yx : footprint;

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
        /// Inside the map and over free cells of the surface only: plain land or plain water, never a deck or a
        /// blocked cell.
        /// </summary>
        public static bool IsValid(in MapSettings map, in NavGrid grid, float3 center, float2 footprint,
            PlacementSurface surface = PlacementSurface.Land)
        {
            var half = new float3(footprint.x * 0.5f, 0f, footprint.y * 0.5f);
            if (!map.Contains(center - half) || !map.Contains(center + half))
            {
                return false;
            }

            return Covers(grid, center, footprint, surface);
        }

        /// <summary>Snaps and grounds <paramref name="position"/> into <paramref name="center"/>, which is set even when the spot is invalid.</summary>
        public static bool Resolve(in MapSettings map, in NavGrid grid, float3 position, float2 footprint,
            PlacementSurface surface, out float3 center)
        {
            center = Snap(map, position, footprint);
            center.y = grid.SurfaceHeight(center);
            return IsValid(map, grid, center, footprint, surface);
        }

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
