using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Sectors;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Fields
{
    /// <summary>
    /// Signed metres from each cell centre to the nearest wall, positive in the open, per
    /// <see cref="MovementClass.FieldKey"/>. Each sector rebuilds over a window with a halo, so its values never depend
    /// on which others were rebuilt.
    /// </summary>
    public struct NavField
    {
        public const float MaxDistance = 8f;
        public const int SectorCells = 16;

        /// <summary>Per field slot, then per cell.</summary>
        public NativeArray<float> Distances;

        /// <summary>Sectors whose window changed since their last rebuild, marked by <see cref="NavGridSystem"/>.</summary>
        public NativeArray<bool> DirtySectors;

        public GridFrame Frame;

        public SectorLayout Layout;

        /// <summary>Cells round a sector that can bring a wall within <see cref="MaxDistance"/> of it.</summary>
        public int ReachCells;

        public static NavField Create(in GridFrame frame, int keys, Allocator allocator)
        {
            var layout = SectorLayout.Covering(frame.Size);
            var field = new NavField
            {
                Distances = new NativeArray<float>(keys * frame.Count, allocator),
                DirtySectors = new NativeArray<bool>(layout.Total, allocator),
                Frame = frame,
                Layout = layout,
                ReachCells = (int)math.ceil(MaxDistance / frame.CellSize + 0.5f),
            };
            field.MarkDirty(int2.zero, frame.Size - 1);
            return field;
        }

        public void Dispose()
        {
            Distances.Dispose();
            DirtySectors.Dispose();
        }

        public readonly int CellCount => Frame.Count;

        /// <summary>Field slots allocated; a new key needs <see cref="Grow"/> and a full build.</summary>
        public readonly int SlotCount => Distances.Length / CellCount;

        /// <summary>The window side every rebuild solves.</summary>
        public readonly int WindowSide => SectorCells + ReachCells * 2;

        /// <summary>The value at a cell centre; a closed half cell off the map.</summary>
        public readonly float At(int slot, int2 cell)
        {
            if (!Frame.Contains(cell))
            {
                return Frame.CellSize * -0.5f;
            }

            return Distances[slot * CellCount + Frame.Index(cell)];
        }

        /// <summary>Whether a cell centre keeps <paramref name="radius"/> from walls, less the shared skin.</summary>
        public readonly bool IsOpen(int slot, int2 cell, float radius) =>
            At(slot, cell) >= radius - NavTolerances.WallOverlap;

        /// <summary>Bilinear between the four nearest cell centres.</summary>
        public readonly float Sample(int slot, float2 position) =>
            Bilinear(new SlotValues(this, slot), Frame, position);

        /// <summary>Bilinear between the four cell centres of a frame nearest a point.</summary>
        internal static float Bilinear<T>(in T values, in GridFrame frame, float2 position)
            where T : struct, ICellValues
        {
            var p = frame.Local(position) - 0.5f;
            var cell = (int2)math.floor(p);
            var t = p - cell;
            var low = math.lerp(values.At(cell), values.At(cell + new int2(1, 0)), t.x);
            var high = math.lerp(values.At(cell + new int2(0, 1)), values.At(cell + 1), t.x);
            return math.lerp(low, high, t.y);
        }

        /// <summary>The way out of the nearest wall: the field's gradient, normalised.</summary>
        public readonly float2 Normal(int slot, float2 position)
        {
            // Central differences half a cell out, so the slope is even on both sides of a cell row.
            var h = Frame.CellSize * 0.5f;
            var dx = Sample(slot, position + new float2(h, 0f)) - Sample(slot, position - new float2(h, 0f));
            var dz = Sample(slot, position + new float2(0f, h)) - Sample(slot, position - new float2(0f, h));
            return math.normalizesafe(new float2(dx, dz));
        }

        /// <summary>Marks every sector whose window holds a cell of the box, so its values get rebuilt.</summary>
        public void MarkDirty(int2 min, int2 max)
        {
            Layout.Range(min, max, ReachCells, out var low, out var high);
            foreach (var sector in new CellRange(low, high))
            {
                DirtySectors[sector.y * Layout.Count.x + sector.x] = true;
            }
        }

        /// <summary>Marks the sectors round every cell whose value differs from before, then copies the values over.</summary>
        public void MarkChanges(in NativeArray<byte> current, NativeArray<byte> previous)
        {
            for (var i = 0; i < current.Length; i++)
            {
                if (current[i] != previous[i])
                {
                    var cell = Frame.Cell(i);
                    MarkDirty(cell, cell);
                    previous[i] = current[i];
                }
            }
        }

        /// <summary>Reallocates for more field slots, keeping the built ones.</summary>
        public void Grow(int keys, Allocator allocator)
        {
            var grown = new NativeArray<float>(keys * CellCount, allocator);
            NativeArray<float>.Copy(Distances, grown, Distances.Length);
            Distances.Dispose();
            Distances = grown;
        }

        /// <summary>
        /// Rebuilds one key's values over one sector from the cells and scenery. Each call writes only that sector's
        /// cells, so calls for different sectors or keys may run at once.
        /// </summary>
        public void Rebuild(in NativeArray<byte> cells, in NativeArray<byte> scenery, in MovementClass key, int slot,
            int sector, ref DistanceTransform transform)
        {
            var first = Layout.Origin(sector);
            var origin = first - ReachCells;
            var side = transform.Side;
            for (var i = 0; i < transform.Closed.Length; i++)
            {
                var cell = origin + new int2(i % side, i / side);
                var index = Frame.Index(cell);
                transform.Closed[i] = !Frame.Contains(cell) || key.Blocks((NavSurface)cells[index], scenery[index]);
            }

            transform.Solve(true);
            Write(slot, first, transform, true);
            transform.Solve(false);
            Write(slot, first, transform, false);
        }

        // From centre distance to edge distance: half a cell less, signed by side, within the cap.
        private void Write(int slot, int2 first, in DistanceTransform transform, bool open)
        {
            var origin = first - ReachCells;
            foreach (var cell in Frame.Cells(first, first + SectorCells - 1))
            {
                var window = (cell.y - origin.y) * transform.Side + (cell.x - origin.x);
                if (transform.Closed[window] == open)
                {
                    continue;
                }

                var edge = math.min((math.sqrt(transform.Squared(window)) - 0.5f) * Frame.CellSize, MaxDistance);
                Distances[slot * CellCount + Frame.Index(cell)] = open ? edge : -edge;
            }
        }

        private readonly struct SlotValues : ICellValues
        {
            private readonly NavField _field;
            private readonly int _slot;

            public SlotValues(in NavField field, int slot)
            {
                _field = field;
                _slot = slot;
            }

            public float At(int2 cell) => _field.At(_slot, cell);
        }
    }
}
