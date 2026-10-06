using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Aircraft helpers on top of <see cref="TestWorld"/>.</summary>
    public static class AircraftTestKit
    {
        public static Entity SpawnAircraft(this TestWorld world, byte faction, float3 position, float altitude = 10f,
            float loiterRadius = 0f, float speed = 10f)
        {
            var aircraft = world.SpawnUnit(faction, position, speed: speed, name: "Aircraft");
            world.EntityManager.SetComponentData(aircraft, new NavAgent { Radius = 0.5f, Layer = NavLayer.Air });
            var sink = new EntityManagerSink(world.EntityManager, aircraft);
            AirSetup.AddFlight(ref sink, new Flight
            {
                Altitude = altitude, ClimbSpeed = 20f, LoiterRadius = loiterRadius,
            });
            return aircraft;
        }
    }
}
