using System.Collections.Generic;
using HyperRTS.Simulation.Navigation.Docks;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Resources.Gathering;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Movement
{
    /// <summary>Trucks gathering between a one-bay drop-off and a four-bay node in open ground.</summary>
    public partial class TrafficScenarioTests
    {
        private const float DockTripSeconds = 300f;

        [Test]
        public void FourTrucks_GatherForFiveMinutes_BetweenOneBayAndFourBays()
        {
            var supplies = _world.CreateSupplies();
            var node = _world.SpawnNode(new float3(30f, 0f, 0f), supplies, 10_000_000);
            DockAuthoring.Add(_world.Writer(node), "Node", NodeBays());
            var depot = _world.SpawnBuilding(1, new float3(-30f, 0f, 0f), new float2(10f, 10f));
            _world.EntityManager.AddComponent<ResourceDropOff>(depot);
            DockAuthoring.Add(_world.Writer(depot), "Depot",
                new List<DockBay> { Bay(new float3(0f, 0f, -8f), new float2(1f, 0f)) });

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
            for (var t = 0f; t < DockTripSeconds; t += TestWorld.FrameTime)
            {
                _record.Tick();
                CountDeliveries(trucks, phases);
            }

            var failures = _world.ReadPathStats().Drive.Failures - before.Drive.Failures;
            _record.Log("dock trips");
            UnityEngine.Debug.Log($"[dock trips] drive failures {failures}, " +
                                           $"searches {_world.ReadPathStats().Drive.Searches - before.Drive.Searches}");

            // Baseline: 5 trips and a 201 degree loop. The final bar is under 180 degrees: at the truck's 11.8 m turn
            // radius a full circle within 40 m reads about 195.
            Assert.GreaterOrEqual(_record.Trips, 5, "the trucks delivered");
            Assert.LessOrEqual(_record.MaxLoop, 210f, "no truck drove a loop");
            Assert.AreEqual(0, failures, "no drive search failed");
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

        // Two bays on each side of the node, each on its own line so no bay's run-in crosses another's.
        private static List<DockBay> NodeBays() => new()
        {
            Bay(new float3(0f, 0f, -8f), new float2(1f, 0f)), Bay(new float3(0f, 0f, -12f), new float2(1f, 0f)),
            Bay(new float3(0f, 0f, 8f), new float2(-1f, 0f)), Bay(new float3(0f, 0f, 12f), new float2(-1f, 0f)),
        };

        private static DockBay Bay(float3 stop, float2 facing) => new()
        {
            Stop = stop, Facing = facing, RunIn = DockAuthoring.DefaultRunIn,
            Exit = stop + new float3(facing.x, 0f, facing.y) * DockAuthoring.DefaultExit,
        };
    }
}
