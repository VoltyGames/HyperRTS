using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Areas;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>
    /// Draws a viewer's <see cref="SightArea"/>: a vision circle that terrain and blockers can occlude. Rays fanned
    /// round the viewer walk outward through each cell they cross, keeping the steepest sight line so far, and a cell
    /// is seen only when nothing nearer rises above it. Woods, crops and smoke the eye is below take a share of sight
    /// per metre crossed, and a ray stops once they have taken it all. Neighbouring rays stay closer than a cell is wide
    /// across them, so they cross every cell in the circle and the cost stays proportional to its area.
    /// </summary>
    internal struct SightLines
    {
        /// <summary>A cell counts as seen when this much above its ground is in sight, like a soldier's top.</summary>
        public const float TargetHeight = 1f;

        /// <summary>Height judged for <see cref="FogOfWar.VisibleTall"/>, like a vehicle's or a building's top.</summary>
        public const float TallTargetHeight = 3f;

        /// <summary>How far above its ground a target's top stands, which sight and direct fire both aim for.</summary>
        public static float TopOf(bool tall) => tall ? TallTargetHeight : TargetHeight;

        // Neighbouring rays' gap at their far end, as a share of a cell's width across them.
        private const float RayGap = 0.9f;

        /// <summary>The grid the area lies on; only its layout is read.</summary>
        public FogOfWar Fog;

        /// <summary>Ground and blockers on the fog grid.</summary>
        public SightBlockers Blockers;

        /// <summary>Flooded terrain hides its bed below this surface; negative infinity when nothing floods.</summary>
        public float WaterFloor;

        /// <summary>The viewer's own block, or the one it garrisons, which hides nothing from it.</summary>
        public NavBox Own;

        public bool HasOwn;

        public SightArea Area;
        public NativeArray<ulong> Bits;

        /// <summary>Draws from an eye <paramref name="eye"/> metres up, over the viewer's position.</summary>
        public void Draw(float3 position, float eye, float range)
        {
            var cellSize = Fog.Grid.CellSize;
            var origin = new float3(position.xz, eye);
            Mark(Fog.Grid.Cell(position), true);

            // Rays reach the far corner of every cell whose centre is in range.
            var length = range + cellSize * 0.5f * math.SQRT2;
            var turn = RayGap * cellSize / length;
            for (var angle = 0f; angle < 2f * math.PI;)
            {
                math.sincos(angle, out var sin, out var cos);
                Cast(origin, new float2(cos, sin) * length, range);
                // A cell is widest across a diagonal ray, so rays there may fan out further.
                angle += turn * (math.abs(sin) + math.abs(cos));
            }
        }

        // origin: viewer XZ in .xy and eye height in .z.
        private void Cast(float3 origin, float2 reach, float range)
        {
            var grid = Fog.Grid;
            var end = origin.xy + reach;
            var length = math.length(reach);
            // A line between two points on the grid never leaves it, but a viewer past the map edge starts off it.
            var fromGrid = grid.Contains(grid.Cell(origin.xy));
            var onGrid = fromGrid && grid.Contains(grid.Cell(end));
            var horizon = float.NegativeInfinity;
            var cover = 0f;
            var ray = new GridRay(grid, origin.xy, end);
            // The viewer's own cell is drawn already.
            ray.MoveNext();
            while (ray.MoveNext())
            {
                if (!onGrid && !grid.Contains(ray.Cell))
                {
                    // The grid is convex: a ray that left it never comes back, one from outside may still enter.
                    if (fromGrid)
                    {
                        break;
                    }

                    continue;
                }

                var point = grid.Center(ray.Cell);
                var distance = math.max(math.distance(point, origin.xy), 1e-3f);
                var cell = Blockers.Cells[grid.Index(ray.Cell)];
                var ground = Surface(cell, point) - origin.z;
                // A cell just out of range still hides what lies behind it.
                if (distance <= range && (ground + TallTargetHeight) / distance >= horizon)
                {
                    Mark(ray.Cell, (ground + TargetHeight) / distance >= horizon);
                }

                horizon = math.max(horizon, ground / distance);
                // Cover takes its share once crossed, so the near edge of a wood is still seen.
                cover += cell.Loss(origin.z - cell.Ground, (ray.Exit - ray.Entry) * length);
                if (cover >= 1f)
                {
                    break;
                }
            }
        }

        /// <summary>The ground or water at a cell, raised by the solid blockers on it.</summary>
        private readonly float Surface(in SightCell cell, float2 point)
        {
            // Most cells hold no solid, so the own-block test runs only where one stands.
            var solid = cell.SolidHeight;
            if (solid > 0f && IsOwn(point))
            {
                solid = 0f;
            }

            return math.max(cell.Ground + solid, WaterFloor);
        }

        // Blocks raise every cell they touch, so the own block reaches cells whose centre lies just outside it.
        private readonly bool IsOwn(float2 point) => HasOwn && Own.Contains(point, Fog.Grid.CellSize * 0.5f * math.SQRT2);

        /// <summary>Marks the cell seen for tall targets, and for every target when <paramref name="ground"/>.</summary>
        private readonly void Mark(int2 cell, bool ground)
        {
            Area.Set(Bits, SightArea.Tall, cell);
            if (ground)
            {
                Area.Set(Bits, SightArea.Ground, cell);
            }
        }
    }
}
