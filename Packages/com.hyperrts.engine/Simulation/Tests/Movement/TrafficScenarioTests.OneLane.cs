using Unity.Entities;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Movement
{
    /// <summary>Traffic from both ends of a stretch only one vehicle wide.</summary>
    public partial class TrafficScenarioTests
    {
        [Test]
        public void TwoColumnsOfTanks_MeetOnAOneLaneBridge()
        {
            _world.SpawnBridge();
            var goals = new float3[8];
            for (var i = 0; i < 4; i++)
            {
                TrackedTank($"east {i}", new float3(-25f - i * 11f, 0f, 0f), East);
                goals[i] = new float3(40f + i * 11f, 0f, 0f);
                TrackedTank($"west {i}", new float3(25f + i * 11f, 0f, 0f), West);
                goals[4 + i] = new float3(-40f - i * 11f, 0f, 0f);
            }

            _world.Tick();
            for (var i = 0; i < 4; i++)
            {
                _world.Send(_record.Units[i * 2].Unit, goals[i]);
                _world.Send(_record.Units[i * 2 + 1].Unit, goals[4 + i]);
            }

            Finish("bridge columns", 180f, 0);
        }

        // A 3 m alley closed at its east end, a squad standing in it, and a truck sent past the squad.
        [Test]
        public void Truck_MeetsASquad_InAThreeMetreAlleyClosedAtTheSquadsEnd()
        {
            _world.SpawnObstacle(new float3(0f, 0f, 11.5f), new float2(80f, 20f));
            _world.SpawnObstacle(new float3(0f, 0f, -11.5f), new float2(80f, 20f));
            _world.SpawnObstacle(new float3(42f, 0f, 0f), new float2(4f, 24f));
            _world.SpawnSquad(1, new float3(25f, 0f, 0f));
            var truck = TrackedTruck("truck", new float3(-55f, 0f, 0f), East);
            _world.Tick();
            _world.Send(truck, new float3(32f, 0f, 0f));
            Finish("alley", 120f, 1);
        }

        private Entity TrackedTank(string name, float3 start, float yaw)
        {
            var tank = SpawnTank(start, yaw);
            _record.Track(name, tank);
            return tank;
        }
    }
}
