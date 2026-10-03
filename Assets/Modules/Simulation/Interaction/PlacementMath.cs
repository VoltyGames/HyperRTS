using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Interaction
{
    /// <summary>Building placement rules shared by the ghost preview and the authoritative placement command.</summary>
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

        /// <summary>Inside the map and, when a grid exists, over free cells only.</summary>
        public static bool IsValid(in MapSettings map, in NavGrid grid, float3 center, float2 footprint)
        {
            var half = new float3(footprint.x * 0.5f, 0f, footprint.y * 0.5f);
            if (!map.Contains(center - half) || !map.Contains(center + half))
            {
                return false;
            }

            return !grid.IsCreated || grid.IsAreaFree(center, footprint);
        }
    }
}
