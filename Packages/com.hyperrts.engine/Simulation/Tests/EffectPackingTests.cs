using HyperRTS.Network.Effects;
using HyperRTS.Simulation.Effects;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Effect events survive the wire, batched under the RPC's capacity.</summary>
    public class EffectPackingTests
    {
        [Test]
        public void PackUnpack_KeepsTheEvent_AndItsDirectionWithinAStep()
        {
            var effect = new EffectEvent
            {
                TypeId = 1234, Slot = EffectSlot.Fire, Faction = 3, Position = new float3(10f, 2f, -5f),
                Direction = math.normalize(new float3(1f, 0.5f, -1f)),
            };

            var read = EffectPacking.Unpack(EffectPacking.Pack(effect));

            Assert.AreEqual(effect.TypeId, read.TypeId);
            Assert.AreEqual(effect.Slot, read.Slot);
            Assert.AreEqual(effect.Faction, read.Faction);
            Assert.AreEqual(effect.Position, read.Position);
            Assert.That(math.dot(effect.Direction, read.Direction), Is.GreaterThan(0.999f));
        }

        [TestCase(0f, 0f, 1f)]
        [TestCase(0f, 0f, -1f)]
        [TestCase(-1f, 0f, 0f)]
        [TestCase(0f, 1f, 0f)]
        [TestCase(0f, -1f, 0f)]
        public void PackUnpack_KeepsAxisDirections(float x, float y, float z)
        {
            var direction = new float3(x, y, z);

            var read = EffectPacking.Unpack(EffectPacking.Pack(new EffectEvent { Direction = direction }));

            Assert.That(math.dot(direction, read.Direction), Is.GreaterThan(0.999f));
        }

        [Test]
        public void Batch_FillsRPCsOf48_AndDropsPastTheLimit()
        {
            using var effects = new NativeArray<EffectEvent>(100, Allocator.Temp);
            using var batches = new NativeList<EffectRpc>(Allocator.Temp);

            EffectPacking.Batch(effects, 3, batches);
            Assert.AreEqual(3, batches.Length);
            Assert.AreEqual(EffectRpc.Capacity, batches[0].Effects.Length);
            Assert.AreEqual(EffectRpc.Capacity, batches[1].Effects.Length);
            Assert.AreEqual(100 - 2 * EffectRpc.Capacity, batches[2].Effects.Length);

            batches.Clear();
            EffectPacking.Batch(effects, EffectSendSystem.MaxBatchesPerTick, batches);
            Assert.AreEqual(2, batches.Length, "a barrage drops the rest");
        }
    }
}
