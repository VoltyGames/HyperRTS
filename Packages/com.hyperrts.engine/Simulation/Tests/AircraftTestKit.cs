using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Orders;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests;

/// <summary>Aircraft and airfield helpers on top of <see cref="TestWorld"/>.</summary>
public static class AircraftTestKit
{
    extension(TestWorld world)
    {
        public Entity SpawnAircraft(byte owner, float3 position, float altitude = 10f,
            float loiterRadius = 0f, float speed = 10f)
        {
            var aircraft = world.SpawnUnit(owner, position, new TestUnit { Speed = speed, Name = "Aircraft" });
            world.Set(aircraft, new NavAgent { Radius = 0.5f, Layer = NavLayer.Air, Mass = 10f });
            var writer = world.Writer(aircraft);
            AirSetup.AddFlight(writer, Agile(altitude, loiterRadius));
            return aircraft;
        }
    }

    /// <summary>Handling quick enough that tests of orders needn't wait on it.</summary>
    public static Flight Agile(float altitude, float loiterRadius = 0f) => new()
    {
        Altitude = altitude,
        ClimbSpeed = 20f,
        LoiterRadius = loiterRadius,
        Acceleration = 20f,
        Braking = 20f,
        MaxBank = math.radians(60f),
        YawRate = math.radians(360f),
    };

    extension(TestWorld world)
    {
        /// <summary>Lets the aircraft take a pad and, with rounds above 0, limits its weapon's ammo.</summary>
        public Entity AddPadUser(Entity aircraft, int rounds = 0, float reloadTime = 0.5f,
            PadKind kind = PadKind.Any)
        {
            var writer = world.Writer(aircraft);
            AirSetup.AddPadUser(writer, kind);
            if (rounds > 0)
            {
                world.SetAmmo(aircraft, rounds, reloadTime, AmmoRefill.Docked);
            }

            return aircraft;
        }
    }

    extension(TestWorld world)
    {
        public Entity SpawnAirfield(byte owner, float3 position, params float3[] pads)
        {
            var airfield = world.SpawnBuilding(owner, position, new float2(4f, 4f), new TestBuilding
            {
                Name = "Airfield",
            });
            var writer = world.Writer(airfield);
            var buffer = AirSetup.AddAirfield(writer);
            foreach (var offset in pads)
            {
                buffer.Add(new LandingPad { Offset = offset });
            }

            return airfield;
        }
    }

    extension(TestWorld world)
    {
        public void ReturnToBase(Entity aircraft, Entity airfield = default) =>
            world.Command(world.Get<Owner>(aircraft).Value, new PlayerCommand
            {
                Type = CommandType.ReturnToBase, Unit = aircraft, Target = airfield,
            });
    }
}
