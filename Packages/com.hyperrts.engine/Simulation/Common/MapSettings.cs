using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Playable area on the XZ plane plus the grid resolutions derived from it.</summary>
    public struct MapSettings : IComponentData
    {
        public float2 Min;
        public float2 Size;
        public float NavCellSize;

        /// <summary>Least clear gap between buildings that don't touch; 0 turns the rule off.</summary>
        public float BuildingGap;

        public float FogCellSize;
        public bool FogOfWar;

        /// <summary>Height of the water surface; ships ride at it.</summary>
        public float WaterLevel;

        /// <summary>Terrain below <see cref="WaterLevel"/> becomes water; otherwise only Water nav areas are.</summary>
        public bool FloodTerrain;

        /// <summary>Steepest walkable ground in degrees; 0 means no limit.</summary>
        public float MaxSlope;

        public readonly float2 Max => Min + Size;

        /// <summary>Middle of the playable area, at height 0.</summary>
        public readonly float3 Center => new(Min.x + Size.x * 0.5f, 0f, Min.y + Size.y * 0.5f);

        /// <summary>Same playable area as <paramref name="other"/>; grids built over one fit the other.</summary>
        public readonly bool SameArea(in MapSettings other) => Min.Equals(other.Min) && Size.Equals(other.Size);

        /// <summary>Same area and nav cells, so a nav-cell grid built over one fits the other.</summary>
        public readonly bool SameNavGrid(in MapSettings other) => SameArea(other) && NavCellSize == other.NavCellSize;

        public readonly bool Contains(float3 position) =>
            math.all(position.xz >= Min) && math.all(position.xz <= Max);

        public readonly float3 Clamp(float3 position) =>
            new(math.clamp(position.x, Min.x, Max.x), position.y, math.clamp(position.z, Min.y, Max.y));

        /// <summary>The point on the playable area's edge nearest <paramref name="position"/>, at its height.</summary>
        public readonly float3 NearestEdgePoint(float3 position)
        {
            var point = Clamp(position);
            var toMin = point.xz - Min;
            var toMax = Max - point.xz;
            var nearest = math.cmin(math.min(toMin, toMax));
            if (nearest == toMin.x)
            {
                return new float3(Min.x, point.y, point.z);
            }

            if (nearest == toMax.x)
            {
                return new float3(Max.x, point.y, point.z);
            }

            return new float3(point.x, point.y, nearest == toMin.y ? Min.y : Max.y);
        }

        /// <summary>Distance from <paramref name="from"/>, inside the area, along unit XZ <paramref name="direction"/> to the edge.</summary>
        public readonly float DistanceToEdge(float3 from, float2 direction)
        {
            var toMax = (Max - from.xz) / direction;
            var toMin = (Min - from.xz) / direction;
            return math.cmin(math.max(toMax, toMin));
        }
    }
}
