using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Heights;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>
    /// Stamps a vision circle that terrain and solid blockers can occlude: rays to every cell on the square around
    /// the viewer walk outward keeping the steepest sight line so far, and a cell is seen only when nothing nearer
    /// rises above it. The rays visit each cell in the square at least once, so the cost stays proportional to the
    /// circle's area.
    /// </summary>
    internal struct SightLines
    {
        /// <summary>A cell counts as seen when this much above its ground is in sight, like a unit's top.</summary>
        public const float TargetHeight = 1f;

        public FogOfWar Fog;
        public TerrainHeight Terrain;

        /// <summary>Solid blockers on the fog grid; none when not created.</summary>
        public SightBlockers Blockers;

        /// <summary>Flooded terrain hides its bed below this surface; negative infinity when nothing floods.</summary>
        public float WaterFloor;

        /// <summary>The viewer's own block, or the one it garrisons, which hides nothing from it.</summary>
        public NavBox Own;

        public bool HasOwn;

        /// <summary>Stamps from an eye <paramref name="eye"/> metres up, over the viewer's position.</summary>
        public void Stamp(float3 position, float eye, float range, ushort bit)
        {
            var center = Fog.WorldToCell(position);
            var radius = (int)math.ceil(range / Fog.CellSize);
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
        private void Cast(float3 origin, int2 center, int2 offset, float range, ushort bit)
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

                var ground = Surface(cell, point) - origin.z;
                if ((ground + TargetHeight) / distance >= horizon)
                {
                    Mark(cell, bit);
                }

                horizon = math.max(horizon, ground / distance);
            }
        }

        /// <summary>The ground or water at a cell, raised by the solid blockers on it.</summary>
        private readonly float Surface(int2 cell, float2 point)
        {
            var ground = Terrain.Height(point);
            if (Blockers.IsCreated && Fog.InBounds(cell) && !IsOwn(point))
            {
                ground += Blockers.SolidHeight(Fog.Index(cell));
            }

            return math.max(ground, WaterFloor);
        }

        // Blocks raise every cell they touch, so the own block reaches cells whose centre lies just outside it.
        private readonly bool IsOwn(float2 point) => HasOwn && Own.Contains(point, Fog.CellSize * 0.5f * math.SQRT2);

        private void Mark(int2 cell, ushort bit)
        {
            if (!Fog.InBounds(cell))
            {
                return;
            }

            var index = Fog.Index(cell);
            Fog.Visible[index] = (ushort)(Fog.Visible[index] | bit);
        }
    }
}
