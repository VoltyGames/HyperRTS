using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// One agent's view of the <see cref="NavGrid"/>: the cells it may enter (its layer's surfaces and the scenery it
    /// drives through) and the square <see cref="Footprint"/> it needs. Paths run over anchors, the low corner cell of
    /// that square, so a footprint of one cell paths over plain cells.
    /// </summary>
    public readonly struct NavAccess
    {
        /// <summary>A <see cref="NavAgent.SceneryCrushLimit"/> that passes all scenery, as infantry walk among trees.</summary>
        public const byte AllScenery = byte.MaxValue;

        public readonly NavGrid Grid;
        public readonly NavSurface Surfaces;
        public readonly byte SceneryCrushLimit;

        /// <summary>Side of the square of cells the agent needs, from its width.</summary>
        public readonly int Footprint;

        /// <summary>Distance kept from closed cells: the radius, capped to fit through a gap of the footprint's width.</summary>
        public readonly float Clearance;

        private readonly int _slot;

        public NavAccess(in NavGrid grid, NavLayer layer, float radius, byte sceneryCrushLimit)
        {
            Grid = grid;
            Surfaces = NavLayers.Surfaces(layer);
            SceneryCrushLimit = sceneryCrushLimit;
            Footprint = math.max(1, (int)math.ceil(radius * 2f / grid.CellSize - 1e-3f));
            Clearance = math.min(radius, (Footprint - 0.1f) * grid.CellSize * 0.5f);
            _slot = (int)layer;
        }

        /// <summary>A one-cell view that only asks a layer's surfaces, for spot searches.</summary>
        public static NavAccess Surface(in NavGrid grid, NavLayer layer) => new(grid, layer, 0f, AllScenery);

        /// <summary>From an anchor to the centre of its footprint square.</summary>
        public float2 Offset => (Footprint - 1) * 0.5f * Grid.CellSize;

        public int2 AnchorAt(float3 position) => Grid.WorldToCell(position.xz - Offset);

        public float3 AnchorCenter(int2 anchor) => Grid.CellCenter(anchor) + new float3(Offset.x, 0f, Offset.y);

        /// <summary>Whether one cell is open to the layer and holds no scenery the agent can't drive through.</summary>
        public bool IsOpen(int2 cell) => IsBlockOpen(cell, 1);

        /// <summary>Whether the agent's whole footprint square, anchored at the cell, is open.</summary>
        public bool IsAnchorOpen(int2 anchor) => IsBlockOpen(anchor, Footprint);

        /// <summary>A diagonal step needs both side squares open, or it would slip between two closed ones.</summary>
        public bool CutsCorner(int2 from, int2 to, int size)
        {
            if (from.x == to.x || from.y == to.y)
            {
                return false;
            }

            return !IsBlockOpen(new int2(from.x, to.y), size) || !IsBlockOpen(new int2(to.x, from.y), size);
        }

        /// <summary>
        /// <see cref="CutsCorner"/> at <see cref="Footprint"/> for a step between two open anchors: each side square
        /// adds only one cell to the from and to squares, so only those two cells need checking.
        /// </summary>
        public bool CutsCornerBetweenOpen(int2 from, int2 to)
        {
            var step = to - from;
            if (step.x == 0 || step.y == 0)
            {
                return false;
            }

            var far = new int2(Footprint - 1);
            var trailing = from + math.select(far, int2.zero, step > 0);
            var leading = to + math.select(int2.zero, far, step > 0);
            return !IsOpen(new int2(trailing.x, leading.y)) || !IsOpen(new int2(leading.x, trailing.y));
        }

        /// <summary>Closest open anchor by ring distance, up to <paramref name="maxRadius"/> rings out.</summary>
        public bool TryFindNearest(int2 anchor, int maxRadius, out int2 found)
        {
            anchor = math.clamp(anchor, 0, Grid.Size - 1);
            found = anchor;
            if (IsAnchorOpen(anchor))
            {
                return true;
            }

            for (var r = 1; r <= maxRadius; r++)
            {
                var best = int.MaxValue;
                for (var i = -r; i <= r; i++)
                {
                    Consider(anchor, new int2(i, -r), ref best, ref found);
                    Consider(anchor, new int2(i, r), ref best, ref found);
                    Consider(anchor, new int2(-r, i), ref best, ref found);
                    Consider(anchor, new int2(r, i), ref best, ref found);
                }

                if (best != int.MaxValue)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True when a straight move stays on open cells without cutting a closed corner. A wide test also keeps
        /// <see cref="Clearance"/> from every closed cell, less the capsule's skin.
        /// </summary>
        public bool HasLineOfSight(float3 from, float3 to, bool wide)
        {
            var clearance = wide ? Clearance : 0f;
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
            var clear = IsSightClear(cell, from.xz, to.xz, clearance);
            for (var i = 0; clear && i < crossings; i++)
            {
                // Through a corner exactly it crosses both axes, and both cells beside the corner must be clear too.
                var axis = new bool2(next.x <= next.y, next.y <= next.x);
                if (math.all(axis))
                {
                    clear = IsSightClear(cell + new int2(step.x, 0), from.xz, to.xz, clearance) &&
                            IsSightClear(cell + new int2(0, step.y), from.xz, to.xz, clearance);
                    i++;
                }

                cell += math.select(int2.zero, step, axis);
                next += math.select(float2.zero, span, axis);
                clear = clear && IsSightClear(cell, from.xz, to.xz, clearance);
            }

            return clear;
        }

        // Exact: only cells within reach of a crossed cell can come within the clearance of the segment.
        private bool IsSightClear(int2 cell, float2 from, float2 to, float clearance)
        {
            if (!IsOpen(cell))
            {
                return false;
            }

            var reach = (int)math.ceil(clearance / Grid.CellSize);
            var corner = cell - reach;
            var side = reach * 2 + 1;
            if (reach == 0 || (SceneryCrushLimit == AllScenery && Grid.ClearanceAt(_slot, corner) >= side))
            {
                return true;
            }

            for (var y = 0; y < side; y++)
            {
                for (var x = 0; x < side; x++)
                {
                    var near = corner + new int2(x, y);
                    if (!IsOpen(near) && Gap(near, from, to) < clearance - NavCapsule.Skin)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private float Gap(int2 cell, float2 from, float2 to)
        {
            var low = Grid.Min + (float2)cell * Grid.CellSize;
            return NavCapsule.SegmentRectDistance(from, to, low, low + Grid.CellSize);
        }

        private bool IsBlockOpen(int2 cell, int size)
        {
            if (!Grid.InBounds(cell))
            {
                return false;
            }

            if (size == 1)
            {
                return IsCellOpen(Grid.Index(cell));
            }

            return Grid.ClearanceAt(_slot, cell) >= size && SceneryFits(cell, size);
        }

        private bool IsCellOpen(int index)
        {
            if (((NavSurface)Grid.Cells[index] & Surfaces) == 0)
            {
                return false;
            }

            return SceneryCrushLimit == AllScenery || Grid.Scenery[index] <= SceneryCrushLimit;
        }

        private bool SceneryFits(int2 anchor, int size)
        {
            if (SceneryCrushLimit == AllScenery)
            {
                return true;
            }

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    if (Grid.Scenery[Grid.Index(anchor + new int2(x, y))] > SceneryCrushLimit)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void Consider(int2 center, int2 offset, ref int best, ref int2 found)
        {
            var distance = offset.x * offset.x + offset.y * offset.y;
            if (distance < best && IsAnchorOpen(center + offset))
            {
                best = distance;
                found = center + offset;
            }
        }
    }
}
