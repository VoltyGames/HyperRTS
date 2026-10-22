using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// Connected regions of a <see cref="NavGrid"/> per clearance layer and footprint: two anchors share a label when
    /// a footprint that wide can path between them, scenery aside. 0 marks a closed anchor.
    /// </summary>
    public struct NavRegions
    {
        /// <summary>Footprints labelled, 1 to this many cells. Wider ones read the widest, which may over-promise.</summary>
        public const int Footprints = 4;

        private const int Slices = NavGrid.ClearanceLayers * Footprints;

        /// <summary>Per layer, then per footprint, then per cell.</summary>
        public NativeArray<int> Labels;

        public static NavRegions Create(int cells, Allocator allocator) =>
            new() { Labels = new NativeArray<int>(cells * Slices, allocator) };

        public void Dispose() => Labels.Dispose();

        /// <summary>The region of an anchor for a footprint on a layer; 0 when closed or off the grid.</summary>
        public readonly int Label(in NavGrid grid, int slot, int footprint, int2 anchor)
        {
            if (!grid.InBounds(anchor))
            {
                return 0;
            }

            var slice = slot * Footprints + math.min(footprint, Footprints) - 1;
            return Labels[slice * grid.Cells.Length + grid.Index(anchor)];
        }

        public void Build(in NavGrid grid)
        {
            for (var slice = 0; slice < Slices; slice++)
            {
                Build(grid, slice);
            }
        }

        /// <summary>Labels every slice at once, one worker each, after the grid's clearance is built.</summary>
        public readonly JobHandle Schedule(in NavGrid grid, JobHandle dependency) =>
            new LabelJob { Cells = grid.Cells, Clearance = grid.Clearance, Size = grid.Size, Labels = Labels }
                .Schedule(Slices, 1, dependency);

        /// <summary>Labels one slice; slices are disjoint, so they may run at once.</summary>
        public void Build(in NavGrid grid, int slice)
        {
            var slot = slice / Footprints;
            var footprint = slice % Footprints + 1;
            var count = grid.Cells.Length;
            var offset = slice * count;
            for (var i = 0; i < count; i++)
            {
                Labels[offset + i] = 0;
            }

            var stack = new NativeList<int>(256, Allocator.Temp);
            var label = 0;
            for (var i = 0; i < count; i++)
            {
                if (Labels[offset + i] == 0 && grid.ClearanceAt(slot, grid.Cell(i)) >= footprint)
                {
                    Labels[offset + i] = ++label;
                    stack.Add(i);
                    Flood(grid, slot, footprint, offset, label, stack);
                }
            }

            stack.Dispose();
        }

        // Four neighbours are enough: a diagonal step needs both side anchors open, so they connect it anyway.
        private void Flood(in NavGrid grid, int slot, int footprint, int offset, int label, NativeList<int> stack)
        {
            while (stack.Length > 0)
            {
                var cell = grid.Cell(stack[stack.Length - 1]);
                stack.RemoveAt(stack.Length - 1);
                Visit(grid, slot, footprint, offset, label, stack, cell + new int2(1, 0));
                Visit(grid, slot, footprint, offset, label, stack, cell - new int2(1, 0));
                Visit(grid, slot, footprint, offset, label, stack, cell + new int2(0, 1));
                Visit(grid, slot, footprint, offset, label, stack, cell - new int2(0, 1));
            }
        }

        private void Visit(in NavGrid grid, int slot, int footprint, int offset, int label, NativeList<int> stack,
            int2 cell)
        {
            if (!grid.InBounds(cell))
            {
                return;
            }

            var index = grid.Index(cell);
            if (Labels[offset + index] == 0 && grid.ClearanceAt(slot, cell) >= footprint)
            {
                Labels[offset + index] = label;
                stack.Add(index);
            }
        }

        [BurstCompile]
        private struct LabelJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<byte> Cells;
            [ReadOnly] public NativeArray<byte> Clearance;
            [NativeDisableParallelForRestriction] public NativeArray<int> Labels;
            public int2 Size;

            // A grid of just the fields labelling reads keeps the rest out of the job.
            public void Execute(int slice)
            {
                var grid = new NavGrid { Cells = Cells, Clearance = Clearance, Size = Size };
                new NavRegions { Labels = Labels }.Build(grid, slice);
            }
        }
    }
}
