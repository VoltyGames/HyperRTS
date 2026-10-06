using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Aircraft and airfield helpers on top of <see cref="TestWorld"/>.</summary>
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

        /// <summary>Lets the aircraft take a pad and, with rounds above 0, limits its weapon's ammo.</summary>
        public static Entity UsePads(this TestWorld world, Entity aircraft, int rounds = 0, float reloadTime = 0.5f)
        {
            var sink = new EntityManagerSink(world.EntityManager, aircraft);
            AirSetup.AddPadUser(ref sink);
            if (rounds > 0)
            {
                WeaponSetup.AddAmmo(ref sink, rounds, reloadTime);
            }

            return aircraft;
        }

        public static Entity SpawnAirfield(this TestWorld world, byte faction, float3 position, params float3[] pads)
        {
            var airfield = world.SpawnBuilding(faction, position, new float2(4f, 4f), name: "Airfield");
            var sink = new EntityManagerSink(world.EntityManager, airfield);
            var buffer = AirSetup.AddAirfield(ref sink);
            foreach (var offset in pads)
            {
                buffer.Add(new LandingPad { Offset = offset });
            }

            return airfield;
        }

        public static void ReturnToBase(this TestWorld world, Entity aircraft, Entity airfield = default) =>
            world.Command(world.Get<Faction>(aircraft).Value, new PlayerCommand
            {
                Type = CommandType.ReturnToBase, Unit = aircraft, Target = airfield,
            });
    }
}
