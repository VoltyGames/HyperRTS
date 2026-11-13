using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources.Gathering;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Movement
{
    /// <summary>
    /// Trucks gathering between a drop-off and a supply field in open ground, each at a spot of its own, turning
    /// round by backing.
    /// </summary>
    [Category("Traffic")]
    public class TrafficGatheringTests : TrafficScenarioFixture
    {
        private const float GatherSeconds = 300f;

        [Test]
        public void FourTrucks_GatherForFiveMinutes_WithoutLoops()
        {
            var trucks = new Entity[4];
            for (var i = 0; i < trucks.Length; i++)
            {
                trucks[i] = SpawnTruck(new float3(-30f + i * 4f, 0f, 16f + i * 3f), math.PI * 0.5f);
            }

            Gather(trucks, new float3(30f, 0f, 0f), new float3(-30f, 0f, 0f), new float2(10f, 10f), GatherSeconds);
            _record.Log("gather trips");

            var numbers = Numbers();
            Assert.Greater(_record.Trips, 15, $"the trucks kept delivering: {numbers}");
            Assert.Less(_record.MaxLoop, 180f, $"no truck turned round forwards: {numbers}; {_record.WorstLoop}");
            Assert.AreEqual(0, _record.GiveUps, $"no truck gave up: {numbers}");
        }

        [Test]
        public void ThreeTrucks_ShuttleToADropOffBesideTheNode_WithoutLoops()
        {
            var trucks = new Entity[3];
            for (var i = 0; i < trucks.Length; i++)
            {
                trucks[i] = SpawnTruck(new float3(14f + i * 4f, 0f, -14f), 0f);
            }

            // The node stands 15 m off the drop-off's corner, closer than a truck can turn round between them.
            Gather(trucks, new float3(22f, 0f, 14f), float3.zero, new float2(14f, 14f), 180f);

            var numbers = Numbers();
            Assert.Greater(_record.Trips, 16, $"each truck takes a spot of its own: {numbers}");
            Assert.Less(_record.MaxLoop, 180f, $"no truck turned round forwards: {numbers}; {_record.WorstLoop}");
            Assert.AreEqual(0, _record.GiveUps, $"no truck gave up: {numbers}");
        }

        [Test]
        public void TrucksOrderedTogether_HeadForSpotsAHullApart()
        {
            var trucks = new[]
            {
                SpawnTruck(new float3(-30f, 0f, 0f), math.PI * 0.5f), SpawnTruck(new float3(-30f, 0f, 6f), math.PI * 0.5f),
            };
            Gather(trucks, new float3(30f, 0f, 0f), new float3(-30f, 0f, -30f), new float2(10f, 10f), 0.5f);

            var first = World.Get<MoveDestination>(trucks[0]).Value.xz;
            var second = World.Get<MoveDestination>(trucks[1]).Value.xz;
            Assert.Greater(math.distance(first, second), 8f, $"spots {first} and {second}");
        }

        // Orders the trucks to gather from a fresh node into a fresh drop-off for the given time.
        private void Gather(Entity[] trucks, float3 nodeAt, float3 depotAt, float2 depotSize, float seconds)
        {
            var node = World.SpawnNode(nodeAt, World.CreateSupplies(), 10_000_000);
            // A supply field's footprint, so trucks spread round it as they do in a match.
            NavSetup.AddObstacle(World.Writer(node), new float2(5f, 5f));
            var depot = World.SpawnBuilding(1, depotAt, depotSize);
            World.EntityManager.AddComponent<ResourceDropOff>(depot);
            var phases = new HarvestPhase[trucks.Length];
            for (var i = 0; i < trucks.Length; i++)
            {
                World.MakeHarvester(trucks[i], 100, 20f);
                World.Order(trucks[i], OrderType.Gather, node);
                _record.Track($"truck {i}", trucks[i]);
            }

            for (var t = 0f; t < seconds; t += TestWorld.FrameTime)
            {
                _record.Tick();
                CountDeliveries(trucks, phases);
            }
        }

        private string Numbers() =>
            $"{_record.Trips} trips, mean {_record.MeanTripSeconds:F1} s, loop {_record.MaxLoop:F0} degrees, " +
            $"give-ups {_record.GiveUps}";

        // A trip ends when a truck turns from carrying its load back to gathering.
        private void CountDeliveries(Entity[] trucks, HarvestPhase[] phases)
        {
            for (var i = 0; i < trucks.Length; i++)
            {
                var phase = World.Get<HarvestState>(trucks[i]).Phase;
                if (phases[i] == HarvestPhase.Returning && phase == HarvestPhase.Gathering)
                {
                    _record.CountTrip(_record.Units[i]);
                }

                phases[i] = phase;
            }
        }
    }
}
