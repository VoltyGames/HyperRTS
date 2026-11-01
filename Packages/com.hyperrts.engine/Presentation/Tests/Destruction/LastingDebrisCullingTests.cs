using HyperRTS.Presentation.Destruction;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Tests.Destruction
{
    /// <summary>Lasting debris culling: pieces outside every split are dropped and runs of one split mask share a draw.</summary>
    public class LastingDebrisCullingTests
    {
        [Test]
        public unsafe void Cull_DropsHiddenPieces_AndJoinsRunsWithOneMask()
        {
            // Split 0 sees x > 0; split 1 sees x < 5.5 and y < 10.
            using var planes = new NativeArray<float4>(new[] { new float4(1, 0, 0, 0), new float4(-1, 0, 0, 5.5f), new float4(0, -1, 0, 10) }, Allocator.Temp);
            using var splits = new NativeArray<CullingSplit>(new[]
            {
                new CullingSplit { cullingPlaneOffset = 0, cullingPlaneCount = 1 },
                new CullingSplit { cullingPlaneOffset = 1, cullingPlaneCount = 2 },
            }, Allocator.Temp);

            // Masks 3, none, 3, 2, 1: the hidden piece does not break the first run.
            using var bounds = new NativeArray<float4>(new[]
            {
                new float4(1, 0, 0, 0.1f), new float4(-5, 20, 0, 0.1f), new float4(2, 0, 0, 0.1f),
                new float4(-5, 0, 0, 0.1f), new float4(8, 0, 0, 0.1f),
            }, Allocator.Temp);
            using var members = new NativeArray<int>(new[] { 0, 1, 2, 3, 4 }, Allocator.Temp);
            using var batches = new NativeArray<LastingDebrisBatch>(new[] { new LastingDebrisBatch { Count = 5 } }, Allocator.Temp);
            using var output = new NativeArray<BatchCullingOutputDrawCommands>(1, Allocator.Temp);
            new LastingDebrisCulling
            {
                Batches = batches, Members = members, Bounds = bounds, Planes = planes, Splits = splits,
                Capacity = 5, Output = output,
            }.Execute();

            var result = output[0];
            try
            {
                Assert.AreEqual(new[] { 0, 2, 3, 4 }, new[] { result.visibleInstances[0], result.visibleInstances[1], result.visibleInstances[2], result.visibleInstances[3] });
                Assert.AreEqual(4, result.visibleInstanceCount);
                Assert.AreEqual(3, result.drawCommandCount);
                Assert.AreEqual((0u, 2u, 3), Run(result.drawCommands[0]));
                Assert.AreEqual((2u, 1u, 2), Run(result.drawCommands[1]));
                Assert.AreEqual((3u, 1u, 1), Run(result.drawCommands[2]));
                Assert.AreEqual(3u, result.drawRanges[0].drawCommandsCount);
            }
            finally
            {
                UnsafeUtility.Free(result.drawCommands, Allocator.TempJob);
                UnsafeUtility.Free(result.visibleInstances, Allocator.TempJob);
                UnsafeUtility.Free(result.drawRanges, Allocator.TempJob);
            }
        }

        private static (uint Offset, uint Count, int Mask) Run(BatchDrawCommand command) =>
            (command.visibleOffset, command.visibleCount, command.splitVisibilityMask);
    }
}
