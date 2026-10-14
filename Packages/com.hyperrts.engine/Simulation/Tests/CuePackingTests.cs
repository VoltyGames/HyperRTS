using HyperRTS.Network.Cues;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Cue events survive the wire, batched under the RPC's capacity.</summary>
    public class CuePackingTests
    {
        [Test]
        public void PackUnpack_KeepsTheEvent_AndItsDirectionWithinAStep()
        {
            var cue = new CueEvent
            {
                TypeId = 1234, Slot = CueSlot.Fire, Faction = 3, Position = new float3(10f, 2f, -5f),
                Direction = math.normalize(new float3(1f, 0.5f, -1f)), Stealthed = true,
            };

            var read = CuePacking.Unpack(CuePacking.Pack(cue));

            Assert.AreEqual(cue.TypeId, read.TypeId);
            Assert.AreEqual(cue.Slot, read.Slot);
            Assert.AreEqual(cue.Faction, read.Faction);
            Assert.AreEqual(cue.Position, read.Position);
            Assert.That(math.dot(cue.Direction, read.Direction), Is.GreaterThan(0.999f));
            Assert.IsTrue(read.Stealthed);
            Assert.IsFalse(CuePacking.Unpack(CuePacking.Pack(default)).Stealthed);
        }

        [TestCase(0f, 0f, 1f)]
        [TestCase(0f, 0f, -1f)]
        [TestCase(-1f, 0f, 0f)]
        [TestCase(0f, 1f, 0f)]
        [TestCase(0f, -1f, 0f)]
        public void PackUnpack_KeepsAxisDirections(float x, float y, float z)
        {
            var direction = new float3(x, y, z);

            var read = CuePacking.Unpack(CuePacking.Pack(new CueEvent { Direction = direction }));

            Assert.That(math.dot(direction, read.Direction), Is.GreaterThan(0.999f));
        }

        [Test]
        public void Batch_FillsRPCsOf48_AndDropsPastTheLimit()
        {
            using var cues = new NativeArray<CueEvent>(100, Allocator.Temp);
            using var batches = new NativeList<CueRpc>(Allocator.Temp);

            CuePacking.Batch(cues, 3, batches);
            Assert.AreEqual(3, batches.Length);
            Assert.AreEqual(CueRpc.Capacity, batches[0].Cues.Length);
            Assert.AreEqual(CueRpc.Capacity, batches[1].Cues.Length);
            Assert.AreEqual(100 - 2 * CueRpc.Capacity, batches[2].Cues.Length);

            batches.Clear();
            CuePacking.Batch(cues, CueSendSystem.MaxBatchesPerTick, batches);
            Assert.AreEqual(2, batches.Length, "a barrage drops the rest");
        }
    }
}
