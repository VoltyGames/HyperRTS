using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources.Gathering;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Movement
{
    /// <summary>Trucks gathering between a drop-off and a node in open ground, each worked from any side.</summary>
    [Category("Traffic")]
    public class TrafficGatheringTests : TrafficScenarioFixture
    {
        private const float GatherSeconds = 300f;

        [Test]
        public void FourTrucks_GatherForFiveMinutes_WithoutLoops()
        {
            var supplies = _world.CreateSupplies();
            var node = _world.SpawnNode(new float3(30f, 0f, 0f), supplies, 10_000_000);
            var depot = _world.SpawnBuilding(1, new float3(-30f, 0f, 0f), new float2(10f, 10f));
            _world.EntityManager.AddComponent<ResourceDropOff>(depot);

            var trucks = new Entity[4];
            var phases = new HarvestPhase[4];
            for (var i = 0; i < trucks.Length; i++)
            {
                trucks[i] = SpawnTruck(new float3(-30f + i * 4f, 0f, 16f + i * 3f), math.PI * 0.5f);
                _world.MakeHarvester(trucks[i], 100, 20f);
                _world.Order(trucks[i], OrderType.Gather, node);
                _record.Track($"truck {i}", trucks[i]);
            }

            var before = _world.ReadPathStats();
            for (var t = 0f; t < GatherSeconds; t += TestWorld.FrameTime)
            {
                _record.Tick();
                CountDeliveries(trucks, phases);
            }

            var failures = _world.ReadPathStats().Drive.Failures - before.Drive.Failures;
            var searches = _world.ReadPathStats().Drive.Searches - before.Drive.Searches;
            _record.Log("gather trips");

            // With dock bays: 5 trips, mean 107.8 s; without: 13, mean 62 s. Each trip turns round at the node and the
            // drop-off, about 180 degrees within the loop window, and a full ring reads 360. Two trucks meeting head-on
            // mid-way still push apart and one nearly circles (333 degrees).
            var numbers = $"{_record.Trips} trips, mean {_record.MeanTripSeconds:F1} s, loop {_record.MaxLoop:F0} degrees, " +
                          $"{failures} of {searches} drive searches failed";
            Assert.Greater(_record.Trips, 5, $"the trucks delivered more often than into bays: {numbers}");
            Assert.Less(_record.MaxLoop, 360f, $"no truck drove a full ring: {numbers}; {_record.WorstLoop}");
            // A failed search leaves its stretch as drawn; 2 of 45 fail, where with bays none of 113 did.
            Assert.LessOrEqual(failures, 2, $"drive searches failed: {numbers}");
        }

        // A trip ends when a truck turns from carrying its load back to gathering.
        private void CountDeliveries(Entity[] trucks, HarvestPhase[] phases)
        {
            for (var i = 0; i < trucks.Length; i++)
            {
                var phase = _world.Get<HarvestState>(trucks[i]).Phase;
                if (phases[i] == HarvestPhase.Returning && phase == HarvestPhase.Gathering)
                {
                    _record.CountTrip(_record.Units[i]);
                }

                phases[i] = phase;
            }
        }
    }
}
