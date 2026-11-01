using HyperRTS.Simulation.Navigation.Areas;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>
    /// Draws a viewer's <see cref="SightPatch"/>: a vision circle that terrain and blockers can occlude: rays to every cell on the square around the viewer
    /// walk outward keeping the steepest sight line so far, and a cell is seen only when nothing nearer rises above
    /// it. Woods, crops and smoke the eye is below take a share of sight per metre, and a ray stops once they have
    /// taken it all. The rays visit each cell in the square at least once, so the cost stays proportional to the
    /// circle's area.
    /// </summary>
    internal struct SightLines
    {
        /// <summary>A cell counts as seen when this much above its ground is in sight, like a soldier's top.</summary>
        public const float TargetHeight = 1f;

        /// <summary>Height judged for <see cref="FogOfWar.VisibleTall"/>, like a vehicle's or a building's top.</summary>
        public const float TallTargetHeight = 3f;

        /// <summary>How far above its ground a target's top stands, which sight and direct fire both aim for.</summary>
        public static float TopOf(bool tall) => tall ? TallTargetHeight : TargetHeight;

        /// <summary>The grid the patch lies on; only its layout is read.</summary>
        public FogOfWar Fog;

        /// <summary>Ground and blockers on the fog grid.</summary>
        public SightBlockers Blockers;

        /// <summary>Flooded terrain hides its bed below this surface; negative infinity when nothing floods.</summary>
        public float WaterFloor;

        /// <summary>The viewer's own block, or the one it garrisons, which hides nothing from it.</summary>
        public NavBox Own;

        public bool HasOwn;

        public SightPatch Patch;
        public NativeArray<ulong> Bits;

        /// <summary>Draws from an eye <paramref name="eye"/> metres up, over the viewer's position.</summary>
        public void Draw(float3 position, float eye, float range)
        {
            var center = Fog.WorldToCell(position);
            var radius = (int)math.ceil(range / Fog.CellSize);
            var origin = new float3(position.xz, eye);
            Mark(center, true);

            for (var i = -radius; i <= radius; i++)
            {
                Cast(origin, center, new int2(i, -radius), range);
                Cast(origin, center, new int2(i, radius), range);
                Cast(origin, center, new int2(-radius, i), range);
                Cast(origin, center, new int2(radius, i), range);
            }
        }

        // origin: viewer XZ in .xy and eye height in .z.
        private void Cast(float3 origin, int2 center, int2 offset, float range)
        {
            var steps = math.cmax(math.abs(offset));
            var horizon = float.NegativeInfinity;
            var cover = 0f;
            var travelled = 0f;
            for (var step = 1; step <= steps; step++)
            {
                var cell = center + (int2)math.round((float2)offset * step / steps);
                var point = Fog.Min + ((float2)cell + 0.5f) * Fog.CellSize;
                var distance = math.max(math.distance(point, origin.xy), 1e-3f);
                // Cells along a ray never get nearer to the viewer, and a ray leaving the map never comes back.
                if (distance > range || !Fog.InBounds(cell))
                {
                    break;
                }

                var index = Fog.Index(cell);
                var terrain = Blockers.Ground[index];
                var ground = Surface(index, point, terrain) - origin.z;
                if ((ground + TallTargetHeight) / distance >= horizon)
                {
                    Mark(cell, (ground + TargetHeight) / distance >= horizon);
                }

                horizon = math.max(horizon, ground / distance);
                // Cover takes its share once crossed, so the near edge of a wood is still seen.
                cover += Blockers.Loss(index, origin.z - terrain, distance - travelled);
                travelled = distance;
                if (cover >= 1f)
                {
                    break;
                }
            }
        }

        /// <summary>The ground or water at a cell, raised by the solid blockers on it.</summary>
        private readonly float Surface(int index, float2 point, float terrain)
        {
            // Most cells hold no solid, so the own-block test runs only where one stands.
            var solid = Blockers.SolidHeight(index);
            if (solid > 0f && IsOwn(point))
            {
                solid = 0f;
            }

            return math.max(terrain + solid, WaterFloor);
        }

        // Blocks raise every cell they touch, so the own block reaches cells whose centre lies just outside it.
        private readonly bool IsOwn(float2 point) => HasOwn && Own.Contains(point, Fog.CellSize * 0.5f * math.SQRT2);

        /// <summary>Marks the cell seen for tall targets, and for every target when <paramref name="ground"/>.</summary>
        private readonly void Mark(int2 cell, bool ground)
        {
            Patch.Set(Bits, SightPatch.Tall, cell);
            if (ground)
            {
                Patch.Set(Bits, SightPatch.Ground, cell);
            }
        }
    }
}
