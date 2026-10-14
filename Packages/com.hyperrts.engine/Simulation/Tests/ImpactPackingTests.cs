using HyperRTS.Network.Combat;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Replays;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Impact events survive packing, batched under the RPC's capacity.</summary>
    public class ImpactPackingTests
    {
        [Test]
        public void PackUnpack_KeepsTheEvent_WithinAPositionStep()
        {
            var impact = new ImpactEvent
            {
                TypeId = 1234, Position = new float3(1500.3f, 12.1f, -1800.7f), Radius = 2.5f, Faction = 3,
                Surface = ImpactSurface.Structure, Material = SurfaceMaterial.Concrete,
            };

            var read = PackedImpact.Pack(impact).Unpack();

            Assert.AreEqual(impact.TypeId, read.TypeId);
            Assert.That(math.distance(impact.Position, read.Position), Is.LessThan(ReplayEntity.PositionStep));
            Assert.AreEqual(impact.Radius, read.Radius, ReplayEntity.PositionStep);
            Assert.AreEqual(impact.Faction, read.Faction);
            Assert.AreEqual(impact.Surface, read.Surface);
            Assert.AreEqual(impact.Material, read.Material);
        }

        [Test]
        public void Batch_FillsRPCsToCapacity_AndDropsPastTheLimit()
        {
            using var impacts = new NativeArray<ImpactEvent>(100, Allocator.Temp);
            using var batches = new NativeList<ImpactRpc>(Allocator.Temp);

            ImpactRpc.Batch(impacts, 2, batches);
            Assert.AreEqual(2, batches.Length);
            Assert.AreEqual(ImpactRpc.Capacity, batches[0].Impacts.Length);
            Assert.AreEqual(100 - ImpactRpc.Capacity, batches[1].Impacts.Length);

            batches.Clear();
            ImpactRpc.Batch(impacts, ImpactSendSystem.MaxBatchesPerTick, batches);
            Assert.AreEqual(1, batches.Length, "a barrage drops the rest");
        }
    }
}
