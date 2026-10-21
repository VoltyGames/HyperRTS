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
        /// True when a straight move stays on open cells without cutting closed corners. A wide test also keeps
        /// <see cref="Clearance"/> from closed cells, with parallel lines under a cell apart so none slips between.
        /// </summary>
        public bool HasLineOfSight(float3 from, float3 to, bool wide)
        {
            var delta = to.xz - from.xz;
            var length = math.length(delta);
            if (!IsSegmentClear(from.xz, to.xz))
            {
                return false;
            }

            if (!wide || length < 1e-4f)
            {
                return true;
            }

            var side = new float2(-delta.y, delta.x) / length;
            var gaps = math.max(2, (int)math.ceil(Clearance * 2f / (Grid.CellSize * 0.9f)));
            for (var i = 0; i <= gaps; i++)
            {
                var offset = side * math.lerp(-Clearance, Clearance, i / (float)gaps);
                if (!IsSegmentClear(from.xz + offset, to.xz + offset))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsSegmentClear(float2 from, float2 to)
        {
            // Quarter-cell samples are fine enough at unit scale and far simpler than an exact grid walk.
            var steps = math.max(1, (int)math.ceil(math.distance(from, to) / (Grid.CellSize * 0.25f)));
            var previous = Grid.WorldToCell(from);
            if (!IsOpen(previous))
            {
                return false;
            }

            for (var i = 1; i <= steps; i++)
            {
                var cell = Grid.WorldToCell(math.lerp(from, to, i / (float)steps));
                if (cell.Equals(previous))
                {
                    continue;
                }

                if (!IsOpen(cell) || CutsCorner(previous, cell, 1))
                {
                    return false;
                }

                previous = cell;
            }

            return true;
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
