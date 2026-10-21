using System.IO;
using HyperRTS.Network.Cues;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Cue events survive the wire and replay files, batched under the RPC's capacity.</summary>
    public class CuePackingTests
    {
        [Test]
        public void PackUnpack_KeepsTheEvent_WithinAPositionStep_AndItsDirectionWithinAStep()
        {
            var cue = new CueEvent
            {
                TypeId = 1234, Slot = CueSlot.Fire, Faction = 3, Position = new float3(1500.3f, 12.1f, -1800.7f),
                Direction = math.normalize(new float3(1f, 0.5f, -1f)), Stealthed = true,
            };

            var read = PackedCue.Pack(cue).Unpack();

            Assert.AreEqual(cue.TypeId, read.TypeId);
            Assert.AreEqual(cue.Slot, read.Slot);
            Assert.AreEqual(cue.Faction, read.Faction);
            Assert.That(math.distance(cue.Position, read.Position), Is.LessThan(QuantizedPosition.Step));
            Assert.That(math.dot(cue.Direction, read.Direction), Is.GreaterThan(0.999f));
            Assert.IsTrue(read.Stealthed);
            Assert.IsFalse(PackedCue.Pack(default).Unpack().Stealthed);
        }

        [Test]
        public void PackUnpack_KeepsAnImpactsRadiusSurfaceAndMaterial()
        {
            var impact = new CueEvent
            {
                TypeId = 1234, Slot = CueSlot.Impact, Position = new float3(3f, 0f, 4f), Radius = 2.5f, Faction = 3,
                Surface = ImpactSurface.Structure, Material = SurfaceMaterial.Concrete,
            };

            var read = PackedCue.Pack(impact).Unpack();

            Assert.AreEqual(impact.Radius, read.Radius, QuantizedPosition.Step);
            Assert.AreEqual(impact.Surface, read.Surface);
            Assert.AreEqual(impact.Material, read.Material);
            Assert.AreEqual(0, read.Variant, "impacts carry their material, not a variant");
        }

        [TestCase(CueSlot.Fire, 3)]
        [TestCase(CueSlot.Announce, AnnouncementKind.GameFirst + 2)]
        public void PackUnpack_KeepsTheVariant(CueSlot slot, int variant)
        {
            var read = PackedCue.Pack(new CueEvent { Slot = slot, Variant = (byte)variant }).Unpack();

            Assert.AreEqual(variant, read.Variant);
            Assert.AreEqual(SurfaceMaterial.None, read.Material);
        }

        [TestCase(0f, 0f, 1f)]
        [TestCase(0f, 0f, -1f)]
        [TestCase(-1f, 0f, 0f)]
        [TestCase(0f, 1f, 0f)]
        [TestCase(0f, -1f, 0f)]
        public void PackUnpack_KeepsAxisDirections(float x, float y, float z)
        {
            var direction = new float3(x, y, z);

            var read = PackedCue.Pack(new CueEvent { Direction = direction }).Unpack();

            Assert.That(math.dot(direction, read.Direction), Is.GreaterThan(0.999f));
        }

        [Test]
        public void WriteRead_RoundTripsIn18Bytes()
        {
            var packed = PackedCue.Pack(new CueEvent
            {
                TypeId = 77, Slot = CueSlot.Death, Faction = 2, Position = new float3(-3f, 1f, 9f), Radius = 4f,
                Direction = new float3(0f, 0f, 1f), Surface = ImpactSurface.Water, Material = SurfaceMaterial.Water,
            });
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            packed.Write(writer);
            stream.Position = 0;

            Assert.AreEqual(18, stream.Length);
            Assert.AreEqual(packed, PackedCue.Read(new BinaryReader(stream)));
        }

        [Test]
        public void Batch_FillsRPCsOf48_AndDropsPastTheLimit()
        {
            using var cues = new NativeArray<CueEvent>(200, Allocator.Temp);
            using var batches = new NativeList<CueRpc>(Allocator.Temp);

            CueRpc.Batch(cues, 2, batches);
            Assert.AreEqual(2, batches.Length);
            Assert.AreEqual(CueRpc.Capacity, batches[0].Cues.Length);
            Assert.AreEqual(CueRpc.Capacity, batches[1].Cues.Length);

            batches.Clear();
            CueRpc.Batch(cues, CueSendSystem.MaxBatchesPerTick, batches);
            Assert.AreEqual(CueSendSystem.MaxBatchesPerTick, batches.Length, "a barrage drops the rest");
        }

        [Test]
        public void Batch_PastTheLimit_DropsFireThenImpacts_AndKeepsArrivalOrder()
        {
            var budget = CueSendSystem.MaxBatchesPerTick * CueRpc.Capacity;
            using var cues = new NativeList<CueEvent>(Allocator.Temp);
            Add(cues, CueSlot.Fire, budget);
            Add(cues, CueSlot.Impact, 20);
            Add(cues, CueSlot.Death, 5);
            Add(cues, CueSlot.Attack, 5);
            using var batches = new NativeList<CueRpc>(Allocator.Temp);

            CueRpc.Batch(cues.AsArray(), CueSendSystem.MaxBatchesPerTick, batches);

            using var sent = new NativeList<CueSlot>(Allocator.Temp);
            foreach (var batch in batches)
            {
                foreach (var packed in batch.Cues)
                {
                    sent.Add(packed.Unpack().Slot);
                }
            }

            Assert.AreEqual(budget, sent.Length);
            Assert.AreEqual(budget - 30, Count(sent, CueSlot.Fire), "fire makes room");
            Assert.AreEqual(20, Count(sent, CueSlot.Impact));
            Assert.AreEqual(5, Count(sent, CueSlot.Death));
            Assert.AreEqual(5, Count(sent, CueSlot.Attack));
            Assert.AreEqual(CueSlot.Fire, sent[0]);
            Assert.AreEqual(CueSlot.Attack, sent[budget - 1], "arrival order is kept");
        }

        private static void Add(NativeList<CueEvent> cues, CueSlot slot, int count)
        {
            for (var i = 0; i < count; i++)
            {
                cues.Add(new CueEvent { TypeId = 1, Slot = slot, Direction = new float3(0f, 0f, 1f) });
            }
        }

        private static int Count(NativeList<CueSlot> slots, CueSlot slot)
        {
            var count = 0;
            foreach (var each in slots)
            {
                count += each == slot ? 1 : 0;
            }

            return count;
        }
    }
}
