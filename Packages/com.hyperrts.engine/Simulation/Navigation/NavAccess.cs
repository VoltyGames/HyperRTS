using System;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// One movement class's view of the <see cref="NavGrid"/>. A cell is open when its centre keeps the class's radius
    /// from walls (<see cref="NavField"/>), and A*, the nearest-cell search and line of sight all use that one rule.
    /// Paths run over cell centres and pay more near walls, up to <see cref="WallMargin"/> away.
    /// </summary>
    public readonly struct NavAccess
    {
        /// <summary>A <see cref="NavAgent.SceneryCrushLimit"/> that passes all scenery, as infantry walk among trees.</summary>
        public const byte AllScenery = byte.MaxValue;

        public readonly NavGrid Grid;
        public readonly MovementClass Class;

        /// <summary>Distance from walls inside which steps cost more; 0 keeps every step at its length.</summary>
        public readonly float WallMargin;

        // Region filter that takes an open cell in any region.
        private const int AnyRegion = -1;

        private readonly int _classIndex;
        private readonly int _slot;

        /// <summary>The view of a registered class; throws for one <see cref="NavFieldSystem"/> hasn't registered.</summary>
        public NavAccess(in NavGrid grid, in MovementClass movementClass, float wallMargin)
        {
            Grid = grid;
            Class = movementClass;
            WallMargin = wallMargin;
            _classIndex = grid.Classes.Find(movementClass);
            if (_classIndex < 0)
            {
                throw new InvalidOperationException("Movement class not registered: no NavAgent has it yet.");
            }

            _slot = grid.Classes.KeySlot(_classIndex);
        }

        /// <summary>Signed metres from the cell centre to the nearest wall, capped at <see cref="NavField.Cap"/>.</summary>
        public float Distance(int2 cell) => Grid.Field.At(_slot, cell);

        /// <summary>Signed metres from a point to the nearest wall, blended between cell centres.</summary>
        public float Distance(float2 position) => Grid.Field.Sample(_slot, position);

        /// <summary>The way out of the nearest wall at a point.</summary>
        public float2 Normal(float2 position) => Grid.Field.Normal(_slot, position);

        /// <summary>Whether the class may stand on the cell's centre.</summary>
        public bool IsOpen(int2 cell) => Grid.InBounds(cell) && Grid.Field.IsOpen(_slot, cell, Class.Radius);

        /// <summary>The connected region of a cell for this class; 0 when closed or off the grid.</summary>
        public int Region(int2 cell) => Grid.Regions.Label(Grid.Field, _classIndex, cell);

        /// <summary>What a step onto the cell costs per metre: 1 in the open, up to 3 against a wall.</summary>
        public float CellCost(int2 cell)
        {
            if (WallMargin <= 0f)
            {
                return 1f;
            }

            return 1f + 2f * math.max(0f, WallMargin - Distance(cell)) / WallMargin;
        }

        /// <summary>Closest open cell by ring distance, up to <paramref name="rings"/> rings out.</summary>
        public bool TryFindNearest(int2 cell, int rings, out int2 found) =>
            TryFindNearest(cell, rings, AnyRegion, out found);

        /// <summary>The same search, keeping only cells in <paramref name="region"/>.</summary>
        public bool TryFindNearest(int2 cell, int rings, int region, out int2 found)
        {
            cell = math.clamp(cell, 0, Grid.Size - 1);
            found = cell;
            if (IsOpenIn(region, cell))
            {
                return true;
            }

            for (var r = 1; r <= rings; r++)
            {
                var best = int.MaxValue;
                for (var i = -r; i <= r; i++)
                {
                    Consider(region, cell, new int2(i, -r), ref best, ref found);
                    Consider(region, cell, new int2(i, r), ref best, ref found);
                    Consider(region, cell, new int2(-r, i), ref best, ref found);
                    Consider(region, cell, new int2(r, i), ref best, ref found);
                }

                if (best != int.MaxValue)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>True when every cell a straight move crosses is open; through a corner, both cells beside it too.</summary>
        public bool HasLineOfSight(float3 from, float3 to)
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
            var clear = IsOpen(cell);
            for (var i = 0; clear && i < crossings; i++)
            {
                var axis = new bool2(next.x <= next.y, next.y <= next.x);
                if (math.all(axis))
                {
                    clear = IsOpen(cell + new int2(step.x, 0)) && IsOpen(cell + new int2(0, step.y));
                    i++;
                }

                cell += math.select(int2.zero, step, axis);
                next += math.select(float2.zero, span, axis);
                clear = clear && IsOpen(cell);
            }

            return clear;
        }

        private void Consider(int region, int2 center, int2 offset, ref int best, ref int2 found)
        {
            var distance = offset.x * offset.x + offset.y * offset.y;
            if (distance < best && IsOpenIn(region, center + offset))
            {
                best = distance;
                found = center + offset;
            }
        }

        private bool IsOpenIn(int region, int2 cell) => (region == AnyRegion || Region(cell) == region) && IsOpen(cell);
    }
}
