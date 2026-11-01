using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// Culls lasting debris for one view: tests each piece's bounding sphere against every split's planes and emits a
    /// draw command per run of a batch's pieces that share one split mask.
    /// </summary>
    [BurstCompile]
    public unsafe struct LastingDebrisCulling : IJob
    {
        [ReadOnly] public NativeArray<LastingDebrisBatch> Batches;

        /// <summary>Each batch's slots, batch b from b * <see cref="Capacity"/>.</summary>
        [ReadOnly] public NativeArray<int> Members;

        /// <summary>Each slot's world bounding sphere, radius in w.</summary>
        [ReadOnly] public NativeArray<float4> Bounds;

        [ReadOnly] public NativeArray<float4> Planes;
        [ReadOnly] public NativeArray<CullingSplit> Splits;
        public int Capacity;
        public BatchID Batch;
        public BatchFilterSettings Filter;

        /// <summary>The view's output; Unity frees the lists the job allocates in it.</summary>
        public NativeArray<BatchCullingOutputDrawCommands> Output;

        public void Execute()
        {
            var pieces = 0;
            foreach (var batch in Batches)
            {
                pieces += batch.Count;
            }

            var output = new BatchCullingOutputDrawCommands
            {
                drawCommands = Allocate<BatchDrawCommand>(pieces),
                visibleInstances = Allocate<int>(pieces),
                drawRanges = Allocate<BatchDrawRange>(1),
            };
            for (var index = 0; index < Batches.Length; index++)
            {
                Emit(index, ref output);
            }

            if (output.drawCommandCount > 0)
            {
                output.drawRanges[0] = new BatchDrawRange
                {
                    drawCommandsType = BatchDrawCommandType.Direct,
                    drawCommandsCount = (uint)output.drawCommandCount,
                    filterSettings = Filter,
                };
                output.drawRangeCount = 1;
            }

            Output[0] = output;
        }

        /// <summary>Bit s is set when <paramref name="sphere"/> touches split s.</summary>
        public readonly uint SplitMask(float4 sphere)
        {
            var mask = 0u;
            for (var split = 0; split < Splits.Length; split++)
            {
                var planes = Planes.GetSubArray(Splits[split].cullingPlaneOffset, Splits[split].cullingPlaneCount);
                if (FrustumPlanes.Intersect(planes, sphere.xyz, sphere.w) != FrustumPlanes.IntersectResult.Out)
                {
                    mask |= 1u << split;
                }
            }

            return mask;
        }

        private void Emit(int index, ref BatchCullingOutputDrawCommands output)
        {
            var batch = Batches[index];
            var start = output.visibleInstanceCount;
            var mask = 0u;
            for (var member = index * Capacity; member < index * Capacity + batch.Count; member++)
            {
                var slot = Members[member];
                var pieceMask = SplitMask(Bounds[slot]);
                if (pieceMask == 0)
                {
                    continue;
                }

                if (pieceMask != mask)
                {
                    Close(batch, mask, start, ref output);
                    start = output.visibleInstanceCount;
                    mask = pieceMask;
                }

                output.visibleInstances[output.visibleInstanceCount++] = slot;
            }

            Close(batch, mask, start, ref output);
        }

        /// <summary>Draws the run of pieces from <paramref name="start"/>; nothing when the run is empty.</summary>
        private readonly void Close(in LastingDebrisBatch batch, uint mask, int start, ref BatchCullingOutputDrawCommands output)
        {
            var count = output.visibleInstanceCount - start;
            if (count == 0)
            {
                return;
            }

            output.drawCommands[output.drawCommandCount++] = new BatchDrawCommand
            {
                visibleOffset = (uint)start,
                visibleCount = (uint)count,
                batchID = Batch,
                materialID = batch.MaterialID,
                meshID = batch.MeshID,
                splitVisibilityMask = (ushort)mask,
            };
        }

        private static T* Allocate<T>(int count) where T : unmanaged =>
            (T*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<T>() * count, UnsafeUtility.AlignOf<T>(), Allocator.TempJob);
    }
}
