using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// Connected regions of a <see cref="NavGrid"/> per <see cref="MovementClass"/>: two cells share a label when the
    /// class can path between them over cells it finds open. 0 marks a closed cell.
    /// </summary>
    public struct NavRegions
    {
        /// <summary>Per class, then per cell.</summary>
        public NativeArray<int> Labels;

        public static NavRegions Create(int classes, int cells, Allocator allocator) =>
            new() { Labels = new NativeArray<int>(classes * cells, allocator) };

        public void Dispose() => Labels.Dispose();

        /// <summary>The region of a cell for a class; 0 when closed or off the grid.</summary>
        public readonly int Label(in NavField field, int classIndex, int2 cell)
        {
            if (math.any(cell < 0 | cell >= field.Size))
            {
                return 0;
            }

            return Labels[classIndex * field.CellCount + cell.y * field.Size.x + cell.x];
        }

        /// <summary>Labels every class in turn, after the field is built.</summary>
        public void Build(in NavField field, in MovementClasses classes)
        {
            for (var i = 0; i < classes.Classes.Length; i++)
            {
                Build(field, classes, i);
            }
        }

        /// <summary>Labels every class at once, one worker each, after the field jobs.</summary>
        public readonly JobHandle Schedule(in NavField field, in MovementClasses classes, JobHandle dependency) =>
            new LabelJob { Field = field, Classes = classes, Labels = Labels }
                .Schedule(classes.Classes.Length, 1, dependency);

        /// <summary>Labels one class; classes are disjoint, so they may run at once.</summary>
        public void Build(in NavField field, in MovementClasses classes, int classIndex)
        {
            var slot = classes.KeySlot(classIndex);
            var radius = classes.Classes[classIndex].Radius;
            var offset = classIndex * field.CellCount;
            for (var i = 0; i < field.CellCount; i++)
            {
                Labels[offset + i] = 0;
            }

            var stack = new NativeList<int2>(256, Allocator.Temp);
            var label = 0;
            for (var i = 0; i < field.CellCount; i++)
            {
                var cell = new int2(i % field.Size.x, i / field.Size.x);
                if (Labels[offset + i] == 0 && field.IsOpen(slot, cell, radius))
                {
                    Labels[offset + i] = ++label;
                    stack.Add(cell);
                    Flood(field, slot, radius, offset, label, stack);
                }
            }

            stack.Dispose();
        }

        // Four neighbours match A*'s eight: a diagonal step needs both side cells open, so they connect it anyway.
        private void Flood(in NavField field, int slot, float radius, int offset, int label, NativeList<int2> stack)
        {
            while (stack.Length > 0)
            {
                var cell = stack[stack.Length - 1];
                stack.RemoveAt(stack.Length - 1);
                Visit(field, slot, radius, offset, label, stack, cell + new int2(1, 0));
                Visit(field, slot, radius, offset, label, stack, cell - new int2(1, 0));
                Visit(field, slot, radius, offset, label, stack, cell + new int2(0, 1));
                Visit(field, slot, radius, offset, label, stack, cell - new int2(0, 1));
            }
        }

        private void Visit(in NavField field, int slot, float radius, int offset, int label, NativeList<int2> stack,
            int2 cell)
        {
            if (math.any(cell < 0 | cell >= field.Size))
            {
                return;
            }

            var index = offset + cell.y * field.Size.x + cell.x;
            if (Labels[index] == 0 && field.IsOpen(slot, cell, radius))
            {
                Labels[index] = label;
                stack.Add(cell);
            }
        }

        [BurstCompile]
        private struct LabelJob : IJobParallelFor
        {
            [ReadOnly] public NavField Field;
            [ReadOnly] public MovementClasses Classes;
            [NativeDisableParallelForRestriction] public NativeArray<int> Labels;

            public void Execute(int classIndex) => new NavRegions { Labels = Labels }.Build(Field, Classes, classIndex);
        }
    }
}
