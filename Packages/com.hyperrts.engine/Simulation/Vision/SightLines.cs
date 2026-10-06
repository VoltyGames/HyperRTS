using HyperRTS.Simulation.Navigation;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>
    /// Stamps a vision circle that terrain can occlude: rays to every cell on the square around the viewer walk
    /// outward keeping the steepest sight line so far, and a cell is seen only when nothing nearer rises above it.
    /// The rays visit each cell in the square at least once, so the cost stays proportional to the circle's area.
    /// </summary>
    internal struct SightLines
    {
        /// <summary>Eye height above the viewer's ground or its own position, whichever is higher.</summary>
        public const float EyeHeight = 2f;

        /// <summary>A cell counts as seen when this much above its ground is in sight, like a unit's top.</summary>
        public const float TargetHeight = 1f;

        public FogOfWar Fog;
        public TerrainHeight Terrain;

        /// <summary>Flooded terrain hides its bed below this surface; negative infinity when nothing floods.</summary>
        public float WaterFloor;

        public void Stamp(float3 position, float range, byte bit)
        {
            var center = Fog.WorldToCell(position);
            var radius = (int)math.ceil(range / Fog.CellSize);
            var eye = math.max(position.y, Terrain.Height(position.xz)) + EyeHeight;
            var origin = new float3(position.xz, eye);
            Mark(center, bit);

            for (var i = -radius; i <= radius; i++)
            {
                Cast(origin, center, new int2(i, -radius), range, bit);
                Cast(origin, center, new int2(i, radius), range, bit);
                Cast(origin, center, new int2(-radius, i), range, bit);
                Cast(origin, center, new int2(radius, i), range, bit);
            }
        }

        // origin: viewer XZ in .xy and eye height in .z.
        private void Cast(float3 origin, int2 center, int2 offset, float range, byte bit)
        {
            var steps = math.cmax(math.abs(offset));
            var horizon = float.NegativeInfinity;
            for (var step = 1; step <= steps; step++)
            {
                var cell = center + (int2)math.round((float2)offset * step / steps);
                var point = Fog.Min + ((float2)cell + 0.5f) * Fog.CellSize;
                var distance = math.max(math.distance(point, origin.xy), 1e-3f);
                // Cells along a ray never get nearer to the viewer, so the rest are out of range too.
                if (distance > range)
                {
                    break;
                }

                var ground = Surface(point) - origin.z;
                if ((ground + TargetHeight) / distance >= horizon)
                {
                    Mark(cell, bit);
                }

                horizon = math.max(horizon, ground / distance);
            }
        }

        private readonly float Surface(float2 point) => math.max(Terrain.Height(point), WaterFloor);

        private void Mark(int2 cell, byte bit)
        {
            if (!Fog.InBounds(cell))
            {
                return;
            }

            var index = Fog.Index(cell);
            Fog.Visible[index] = (byte)(Fog.Visible[index] | bit);
        }
    }
}
