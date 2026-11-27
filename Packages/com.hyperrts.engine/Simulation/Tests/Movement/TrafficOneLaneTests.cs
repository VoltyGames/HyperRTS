using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Movement;

/// <summary>Traffic from both ends of a stretch only one vehicle wide.</summary>
[Category("Traffic")]
public class TrafficOneLaneTests : TrafficScenarioFixture
{
    // A 3 m alley closed at its east end, a squad standing in it, and a truck sent past the squad.
    [Test]
    public void Truck_MeetsASquad_InAThreeMetreAlleyClosedAtTheSquadsEnd()
    {
        World.SpawnObstacle(new float3(0f, 0f, 11.5f), new float2(80f, 20f));
        World.SpawnObstacle(new float3(0f, 0f, -11.5f), new float2(80f, 20f));
        World.SpawnObstacle(new float3(42f, 0f, 0f), new float2(4f, 24f));
        World.SpawnSquad(1, new float3(25f, 0f, 0f));
        var truck = TrackedTruck("truck", new float3(-55f, 0f, 0f), East);
        World.Tick();
        World.Send(truck, new float3(32f, 0f, 0f));
        Finish("alley", 120f, 1);
    }
}
