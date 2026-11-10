using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Death;
using HyperRTS.Simulation.Movement.Crushing;
using HyperRTS.Simulation.Navigation.Agents;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Movement
{
    /// <summary>Vehicles drive over enemy foot units: a fast crusher kills the squad members under its hull and never slows for them.</summary>
    public class CrushTests : SimulationFixture
    {
        private const float East = math.PI * 0.5f;
        private const int CrushedByTank = 1;
        private const int SquadSize = 4;

        // Owners 1 and 2 are teammates; owner 3 is the enemy.
        protected override byte[] Teams => new byte[] { 1, 1, 2 };

        private Entity SpawnTank(float speed, int crusherSize = 3)
        {
            var tank = World.SpawnVehicle(1, new float3(-30f, 0f, 0f), East, speed, TestVehicles.TankHull(),
                TestVehicles.Tracks);
            if (crusherSize > 0)
            {
                World.Add(tank, new Crusher { Size = crusherSize });
            }

            return tank;
        }

        private Entity SpawnSquad(byte owner)
        {
            var squad = World.SpawnSquad(owner, float3.zero, SquadSize);
            World.Add(squad, new Crushable { Crush = CrushedByTank });
            return squad;
        }

        private void DriveThrough(Entity tank, out float slowest)
        {
            World.Tick();
            World.Set(tank, new MoveDestination { Value = new float3(30f, 0f, 0f) });
            World.SetEnabled<MoveDestination>(tank, true);
            slowest = float.MaxValue;
            for (var t = 0f; t < 120f && World.IsEnabled<MoveDestination>(tank); t += TestWorld.FrameTime)
            {
                World.Tick();
                var speed = math.length(World.Get<Velocity>(tank).Value);
                var abreast = math.abs(World.GetPosition(tank).x) < 8f;
                slowest = abreast ? math.min(slowest, speed) : slowest;
            }
        }

        // A squad with no living member is gone from the world.
        private int Dead(Entity squad)
        {
            if (!World.EntityManager.Exists(squad))
            {
                return SquadSize;
            }

            var members = World.Buffer<SquadMember>(squad);
            var dead = 0;
            for (var i = 0; i < members.Length; i++)
            {
                dead += members[i].Alive ? 0 : 1;
            }

            return dead;
        }

        [TestCase(7f, true)]
        [TestCase(0.8f, false)]
        public void Tank_CrushesEnemyMembersUnderIt_OnlyAboveOneMetrePerSecond(float top, bool kills)
        {
            var tank = SpawnTank(top);
            var squad = SpawnSquad(3);

            DriveThrough(tank, out var slowest);

            if (!kills)
            {
                Assert.AreEqual(0, Dead(squad), "a crusher below 1 m/s crushes nothing");
                return;
            }

            Assert.Greater(Dead(squad), 0, "the members under the hull died");
            Assert.Greater(slowest, top * 0.5f, $"it never slowed for the squad, slowest {slowest:F2} m/s");
            Assert.IsFalse(World.IsEnabled<MoveDestination>(tank), "the tank arrived");
            var killer = World.EntityManager.Exists(squad) ? World.Get<LastAttacker>(squad).Owner : (byte)1;
            Assert.AreEqual(1, killer, "the crusher's owner gets the kills");
            Assert.AreEqual(Dead(squad) == SquadSize ? 1 : 0, World.Get<PlayerStats>(World.Player(1)).UnitsKilled);
        }

        [Test]
        public void Truck_WithoutACrusher_TreatsTheSquadAsAnObstacle()
        {
            var truck = World.SpawnVehicle(1, new float3(-30f, 0f, 0f), East, 6f, TestVehicles.SupplyTruckHull(),
                TestVehicles.SupplyTruck);
            var squad = SpawnSquad(3);

            DriveThrough(truck, out _);

            Assert.AreEqual(0, Dead(squad), "nothing crushed the squad");
        }

        [Test]
        public void Tank_NeverCrushesATeammatesSquad()
        {
            var tank = SpawnTank(7f);
            var squad = SpawnSquad(2);

            DriveThrough(tank, out _);

            Assert.AreEqual(0, Dead(squad), "an allied squad is never crushed");
        }
    }
}
