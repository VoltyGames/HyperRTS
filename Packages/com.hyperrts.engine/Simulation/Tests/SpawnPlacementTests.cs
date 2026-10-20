using HyperRTS.Simulation.Common;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    public class SpawnPlacementTests
    {
        [Test]
        public void Place_WritesTheWorldPoseTheTransformSystemsWouldCompute()
        {
            using var world = new World("Spawn Placement Tests");
            var em = world.EntityManager;
            var spawned = em.CreateEntity(typeof(LocalTransform), typeof(LocalToWorld));
            var transform = LocalTransform.FromPositionRotationScale(new float3(40f, 2f, -25f),
                quaternion.RotateY(1.2f), 1.5f);
            var stretch = float4x4.Scale(1f, 3f, 1f);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            ecb.Place(spawned, transform, stretch);
            ecb.AddComponent(spawned, new PostTransformMatrix { Value = stretch });
            ecb.Playback(em);
            var placed = em.GetComponentData<LocalToWorld>(spawned).Value;

            world.GetOrCreateSystem<LocalToWorldSystem>().Update(world.Unmanaged);
            var computed = em.GetComponentData<LocalToWorld>(spawned).Value;

            for (var c = 0; c < 4; c++)
            {
                Assert.That(math.distance(placed[c], computed[c]), Is.LessThan(1e-4f), $"column {c}");
            }

            Assert.AreEqual(transform, em.GetComponentData<LocalTransform>(spawned));
        }
    }
}
