using System;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// One movement class's view of the <see cref="NavGrid"/>: which cells it may stand on, one rule shared by A*, the
    /// nearest-cell search and line of sight, and what steps onto them cost.
    /// </summary>
    public readonly struct NavAccess
    {
        /// <summary>A <see cref="NavAgent.SceneryCrushLimit"/> that passes all scenery, as infantry walk among trees.</summary>
        public const byte AllScenery = byte.MaxValue;

        public readonly NavGrid Grid;
        public readonly MovementClass Class;

        /// <summary>Distance from walls inside which steps cost more; 0 keeps every step at its length.</summary>
        public readonly float WallMargin;

        /// <summary>The class's index in <see cref="MovementClasses"/>, which lays out its sector graph.</summary>
        public readonly int ClassIndex;

        // Region filter that takes an open cell in any region.
        private const int AnyRegion = -1;

        // Clearance for a nearest-cell search that needs no line to the cell.
        private const float AnyClearance = float.NegativeInfinity;

        private readonly int _slot;

        /// <summary>The view of a registered class; throws for one <see cref="NavFieldSystem"/> hasn't registered.</summary>
        public NavAccess(in NavGrid grid, in MovementClass movementClass, float wallMargin)
        {
            Grid = grid;
            Class = movementClass;
            WallMargin = wallMargin;
            ClassIndex = grid.Classes.Find(movementClass);
            if (ClassIndex < 0)
            {
                throw new InvalidOperationException("Movement class not registered: no NavAgent has it yet.");
            }

            _slot = grid.Classes.FieldSlot(ClassIndex);
        }

        /// <summary>Signed metres from the cell centre to the nearest wall, at most <see cref="NavField.MaxDistance"/>.</summary>
        public float Distance(int2 cell) => Grid.Field.At(_slot, cell);

        /// <summary>Signed metres from a point to the nearest wall, blended between cell centres.</summary>
        public float Distance(float2 position) => Grid.Field.Sample(_slot, position);

        /// <summary>The way out of the nearest wall at a point.</summary>
        public float2 Normal(float2 position) => Grid.Field.Normal(_slot, position);

        /// <summary>Whether the class may stand on the cell's centre.</summary>
        public bool IsOpen(int2 cell) => Grid.InBounds(cell) && Grid.Field.IsOpen(_slot, cell, Class.Radius);

        /// <summary>The connected region of a cell for this class; 0 when closed or off the grid.</summary>
        public int Region(int2 cell) => Grid.Sectors.Region(ClassIndex, cell);

        /// <summary>What a step onto the cell costs per metre: 1 in the open, up to 3 against a wall.</summary>
        public float CellCost(int2 cell) => CellCost(Distance(cell), WallMargin);

        /// <summary><see cref="CellCost(int2)"/> from a field value already read.</summary>
        public static float CellCost(float distance, float wallMargin)
        {
            if (wallMargin <= 0f)
            {
                return 1f;
            }

            return 1f + 2f * math.max(0f, wallMargin - distance) / wallMargin;
        }

        /// <summary>The least field value a cell may read and stay open to the class.</summary>
        public float OpenDistance => Class.Radius - NavTolerances.WallOverlap;

        /// <summary>Closest open cell by ring distance, up to <paramref name="rings"/> rings out.</summary>
        public bool TryFindNearest(int2 cell, int rings, out int2 found) =>
            TryFindNearest(cell, rings, AnyRegion, out found);

        /// <summary>The same search, keeping only cells in <paramref name="region"/>.</summary>
        public bool TryFindNearest(int2 cell, int rings, int region, out int2 found) =>
            TryFindNearest(cell, rings, new NearestFilter(region, float3.zero, AnyClearance), out found);

        /// <summary>
        /// Where a path from <paramref name="start"/> in <paramref name="startCell"/> begins: the cell when open, else
        /// the nearest open cell it can squeeze to in the goal's region, then in any region, then the nearest open
        /// cell, which is how a unit inside a footprint gets out.
        /// </summary>
        public bool TryFindStart(int2 startCell, float3 start, float3 goal, out int2 from)
        {
            var rings = GridPathfinder.NearestSearchRadius;
            startCell = math.clamp(startCell, 0, Grid.Size - 1);
            if (IsOpen(startCell))
            {
                from = startCell;
                return true;
            }

            // A unit already in a gap narrower than its class may drive along it, but never through anything tighter.
            var squeeze = math.min(OpenDistance, Distance(startCell));
            if (squeeze > 0f)
            {
                var goalRegion = NearestRegion(goal);
                if (goalRegion != 0 &&
                    TryFindNearest(startCell, rings, new NearestFilter(goalRegion, start, squeeze), out from))
                {
                    return true;
                }

                if (TryFindNearest(startCell, rings, new NearestFilter(AnyRegion, start, squeeze), out from))
                {
                    return true;
                }
            }

            return TryFindNearest(startCell, rings, out from);
        }

        /// <summary>The region of the open cell nearest <paramref name="point"/>; 0 when none is near.</summary>
        public int NearestRegion(float3 point)
        {
            var cell = Grid.WorldToCell(point);
            return TryFindNearest(cell, GridPathfinder.NearestSearchRadius, out var open) ? Region(open) : 0;
        }

        // Ring search; with a clearance, a cell also needs a straight line to it from the origin that keeps it.
        private bool TryFindNearest(int2 cell, int rings, in NearestFilter filter, out int2 found)
        {
            cell = math.clamp(cell, 0, Grid.Size - 1);
            found = cell;
            if (IsCandidate(filter, cell))
            {
                return true;
            }

            for (var r = 1; r <= rings; r++)
            {
                var best = int.MaxValue;
                for (var i = -r; i <= r; i++)
                {
                    Consider(filter, cell, new int2(i, -r), ref best, ref found);
                    Consider(filter, cell, new int2(i, r), ref best, ref found);
                    Consider(filter, cell, new int2(-r, i), ref best, ref found);
                    Consider(filter, cell, new int2(r, i), ref best, ref found);
                }

                if (best != int.MaxValue)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>True when every cell a straight move crosses is open; through a corner, both cells beside it too.</summary>
        public bool HasLineOfSight(float3 from, float3 to) => HasLineOfSight(from, to, OpenDistance);

        /// <summary>The same walk, each cell reading at least <paramref name="clearance"/> from walls.</summary>
        public bool HasLineOfSight(float3 from, float3 to, float clearance)
        {
            var start = (from.xz - Grid.Min) / Grid.CellSize;
            var delta = (to.xz - Grid.Min) / Grid.CellSize - start;
            var cell = (int2)math.floor(start);
            var crossings = math.csum(math.abs((int2)math.floor(start + delta) - cell));
            var step = (int2)math.sign(delta);

            // Amanatides and Woo: the line's parameter per cell crossed, and where it next crosses each axis.
            var still = delta == 0f;
            var span = math.select(1f / math.abs(delta), float.PositiveInfinity, still);
            var toEdge = math.select((float2)cell + 1f - start, start - cell, delta < 0f);
            var next = math.select(toEdge * span, float.PositiveInfinity, still);
            var clear = IsClear(cell, clearance);
            for (var i = 0; clear && i < crossings; i++)
            {
                var axis = new bool2(next.x <= next.y, next.y <= next.x);
                if (math.all(axis))
                {
                    var sides = IsClear(cell + new int2(step.x, 0), clearance);
                    clear = sides && IsClear(cell + new int2(0, step.y), clearance);
                    i++;
                }

                cell += math.select(int2.zero, step, axis);
                next += math.select(float2.zero, span, axis);
                clear = clear && IsClear(cell, clearance);
            }

            return clear;
        }

        private void Consider(in NearestFilter filter, int2 center, int2 offset, ref int best, ref int2 found)
        {
            var distance = offset.x * offset.x + offset.y * offset.y;
            if (distance < best && IsCandidate(filter, center + offset))
            {
                best = distance;
                found = center + offset;
            }
        }

        private bool IsCandidate(in NearestFilter filter, int2 cell)
        {
            if (!IsOpenIn(filter.Region, cell))
            {
                return false;
            }

            return filter.Clearance == AnyClearance ||
                   HasLineOfSight(filter.Origin, Grid.CellCenter(cell), filter.Clearance);
        }

        // Which cells a ring search keeps: open ones in the region, and with a clearance only those a straight line
        // from the origin reaches keeping it.
        private readonly struct NearestFilter
        {
            public readonly int Region;
            public readonly float3 Origin;
            public readonly float Clearance;

            public NearestFilter(int region, float3 origin, float clearance)
            {
                Region = region;
                Origin = origin;
                Clearance = clearance;
            }
        }

        private bool IsOpenIn(int region, int2 cell) => (region == AnyRegion || Region(cell) == region) && IsOpen(cell);

        private bool IsClear(int2 cell, float clearance) => Grid.InBounds(cell) && Distance(cell) >= clearance;
    }
}
