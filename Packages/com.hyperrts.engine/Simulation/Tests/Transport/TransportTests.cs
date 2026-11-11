using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common.Events;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Cues.Sounds;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Transport;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Simulation.Tests.Transport
{
    /// <summary>Boarding, carrying, firing out, unloading and container death.</summary>
    public class TransportTests : SimulationFixture
    {
        private Entity Passenger(float3 position, int size = 1, bool armed = false)
        {
            var unit = World.SpawnUnit(1, position);
            var writer = World.Writer(unit);
            TransportSetup.AddPassenger(writer, size);
            if (armed)
            {
                World.AddWeapon(unit, new TestWeapon { Range = 3f, Damage = 10f });
            }

            return unit;
        }

        private Entity Bunker(float3 position, int capacity = 2, bool fire = false, bool survive = true)
        {
            var bunker = World.SpawnBuilding(1, position, new float2(4f, 4f));
            var writer = World.Writer(bunker);
            TransportSetup.AddContainer(writer, new Container
            {
                Capacity = capacity, MaxPassengerSize = 2, PassengersFire = fire, PassengersSurvive = survive,
            });
            return bunker;
        }

        [Test]
        public void SmartClick_Boards_UpToCapacity()
        {
            var bunker = Bunker(float3.zero, capacity: 2);
            var a = Passenger(new float3(8f, 0f, 0f));
            var b = Passenger(new float3(8f, 0f, 2f));
            var c = Passenger(new float3(8f, 0f, -2f));
            World.Enter(a, bunker);
            World.Enter(b, bunker);
            World.Enter(c, bunker);

            World.Run(4f);

            Assert.AreEqual(2, World.Buffer<Cargo>(bunker).Length);
            Assert.IsTrue(World.IsInside(a) && World.IsInside(b));
            Assert.IsFalse(World.IsInside(c), "no room left");
            Assert.IsFalse(World.IsEnabled<ActiveOrder>(c), "the order ends when full");
        }

        [Test]
        public void Infantry_BoardsALongHull_FromBehind()
        {
            var carrier = World.SpawnUnit(1, float3.zero, new TestUnit { Radius = 1.5f });
            World.Set(carrier, LocalTransform.FromPositionRotation(float3.zero,
                quaternion.RotateY(math.PI * 0.5f)));
            var agent = World.Get<NavAgent>(carrier);
            agent.HullLength = 7f;
            World.Set(carrier, agent);
            var writer = World.Writer(carrier);
            TransportSetup.AddContainer(writer, new Container { Capacity = 4, MaxPassengerSize = 1 });
            var rider = Passenger(new float3(-12f, 0f, 0f));

            World.Enter(rider, carrier);
            World.Run(5f);

            Assert.IsTrue(World.IsInside(rider), "boards at the rear of the hull");
        }

        [Test]
        public void Container_RidingInAnother_TakesNoPassengers()
        {
            var ship = Bunker(float3.zero, capacity: 4);
            var truck = World.SpawnUnit(1, new float3(5f, 0f, 0f), new TestUnit { Radius = 1f });
            var writer = World.Writer(truck);
            TransportSetup.AddContainer(writer, new Container { Capacity = 4, MaxPassengerSize = 1 });
            TransportSetup.AddPassenger(writer, 2);
            World.Enter(truck, ship);
            World.Run(2f);
            Assert.IsTrue(World.IsInside(truck));

            var rider = Passenger(new float3(8f, 0f, 0f));
            World.Command(1, new PlayerCommand { Type = CommandType.Enter, Unit = rider, Target = truck });
            World.Run(2f);

            Assert.IsFalse(World.IsInside(rider));
            Assert.AreEqual(0, World.Buffer<Cargo>(truck).Length);
        }

        [Test]
        public void Passengers_RideAlong_AndIgnoreOrders()
        {
            var truck = World.SpawnUnit(1, float3.zero, new TestUnit { Speed = 5f, Radius = 1f });
            var writer = World.Writer(truck);
            TransportSetup.AddContainer(writer, new Container { Capacity = 4, MaxPassengerSize = 1 });
            var rider = Passenger(new float3(4f, 0f, 0f));
            World.Enter(rider, truck);
            World.Run(2f);
            Assert.IsTrue(World.IsInside(rider));

            World.Command(1, new PlayerCommand { Type = CommandType.Move, Unit = truck, Position = new float3(30f, 0f, 0f) });
            World.Command(1, new PlayerCommand { Type = CommandType.Move, Unit = rider, Position = new float3(-30f, 0f, 0f) });
            World.Run(8f);

            var truckPosition = World.Get<LocalTransform>(truck).Position;
            Assert.Greater(truckPosition.x, 25f);
            Assert.AreEqual(truckPosition.x, World.Get<LocalTransform>(rider).Position.x, 1e-3f);
        }

        [Test]
        public void Garrison_FiresOut_ButIsNotTargetable()
        {
            var bunker = Bunker(float3.zero, fire: true);
            var rifle = Passenger(new float3(6f, 0f, 0f), armed: true);
            World.Enter(rifle, bunker);
            World.Run(2f);

            var enemy = World.SpawnUnit(2, new float3(0f, 0f, 4.5f), new TestUnit { Speed = 0f });
            World.AddWeapon(enemy, new TestWeapon { Stance = Stance.HoldPosition, Range = 3f, Damage = 10f });
            World.Run(2f);

            Assert.Less(World.Get<Health>(enemy).Current, 100f, "fires from the bunker's edge");
            Assert.AreEqual(100f, World.Get<Health>(rifle).Current, "passengers can't be shot");
            Assert.AreEqual(Stance.HoldPosition, World.Get<CombatStance>(rifle).Value);
        }

        /// <summary>A garrisoned rifle with a voiced projectile weapon and an enemy east of the bunker; ports on both walls if asked.</summary>
        private Entity GarrisonFiringEast(bool ports)
        {
            var bunker = Bunker(float3.zero, fire: true);
            if (ports)
            {
                var buffer = World.EntityManager.AddBuffer<FiringPort>(bunker);
                buffer.Add(new FiringPort { Position = new float3(-2f, 0f, 0f), Direction = new float3(-1f, 0f, 0f) });
                buffer.Add(new FiringPort { Position = new float3(2f, 0f, 0f), Direction = new float3(1f, 0f, 0f) });
            }

            var rifle = Passenger(new float3(0f, 0f, 6f));
            World.AddWeapon(rifle, new TestWeapon { Range = 3f, Damage = 10f, Projectile = World.ProjectilePrefab() });
            var writer = World.Writer(rifle);
            var sound = World.Own(ScriptableObject.CreateInstance<SoundCue>());
            EntitySoundsAuthoring.Set(EntitySoundsAuthoring.Add(writer), CueSlot.Fire, sound);
            World.Enter(rifle, bunker);
            World.Run(2f);
            World.SpawnUnit(2, new float3(4.5f, 0f, 0f), new TestUnit { Speed = 0f });
            return rifle;
        }

        /// <summary>Where the first projectile starts; <paramref name="fireCue"/> is the Fire cue of the same frame.</summary>
        private float3 FirstShotStart(out CueEvent fireCue)
        {
            fireCue = default;
            using var query = World.Query(typeof(Projectile));
            for (var t = 0f; t < 2f; t += TestWorld.FrameTime)
            {
                World.Tick();
                if (query.CalculateEntityCount() > 0)
                {
                    var cues = World.Queued(CueSlot.Fire);
                    Assert.IsNotEmpty(cues);
                    fireCue = cues[0];
                    return World.Get<Projectile>(query.GetSingletonEntity()).Start;
                }
            }

            Assert.Fail("nothing fired");
            return default;
        }

        [Test]
        public void Garrison_FiresFromThePortFacingItsTarget()
        {
            GarrisonFiringEast(ports: true);

            var start = FirstShotStart(out var cue);

            Assert.AreEqual(2f, start.x, 0.01f, "the shot leaves the east window");
            Assert.AreEqual(2f, cue.Position.x, 0.01f);
            Assert.Greater(cue.Direction.x, 0.9f, "faces the target");
        }

        [Test]
        public void Garrison_WithoutPorts_FiresFromItsCentre()
        {
            GarrisonFiringEast(ports: false);

            Assert.AreEqual(0f, FirstShotStart(out _).x, 0.01f);
        }

        [Test]
        public void Unload_LetsEveryoneOut_AndRestoresStance()
        {
            var bunker = Bunker(float3.zero);
            var rifle = Passenger(new float3(6f, 0f, 0f), armed: true);
            World.Enter(rifle, bunker);
            World.Run(2f);

            World.Command(1, new PlayerCommand { Type = CommandType.Unload, Unit = bunker, Argument = -1 });
            World.Tick();

            Assert.IsFalse(World.IsInside(rifle));
            Assert.AreEqual(0, World.Buffer<Cargo>(bunker).Length);
            Assert.AreEqual(Stance.Aggressive, World.Get<CombatStance>(rifle).Value);
            Assert.Greater(math.length(World.Get<LocalTransform>(rifle).Position.xz), 2f, "outside the footprint");
        }

        [Test]
        public void Unload_StepsOutAtTheExit_TurnedWithTheContainer()
        {
            var bunker = Bunker(float3.zero);
            var container = World.Get<Container>(bunker);
            container.HasExit = true;
            container.Exit = new float3(0f, 0f, -3f);
            World.Set(bunker, container);
            World.Set(bunker, LocalTransform.FromRotation(quaternion.RotateY(math.PI)));
            var rifle = Passenger(new float3(6f, 0f, 0f));
            World.Enter(rifle, bunker);
            World.Run(2f);

            World.Command(1, new PlayerCommand { Type = CommandType.Unload, Unit = bunker, Argument = -1 });
            World.Tick();

            var position = World.Get<LocalTransform>(rifle).Position;
            Assert.IsFalse(World.IsInside(rifle));
            Assert.Less(math.distance(position.xz, new float2(0f, 3f)), 2f, "at the exit, behind the turned bunker");
        }

        [Test]
        public void DefeatedPlayer_CannotUnload()
        {
            var bunker = Bunker(float3.zero);
            var rifle = Passenger(new float3(6f, 0f, 0f));
            World.Enter(rifle, bunker);
            World.Run(2f);
            World.SetEnabled<Defeated>(World.Player(1), true);

            World.Command(1, new PlayerCommand { Type = CommandType.Unload, Unit = bunker, Argument = -1 });
            World.World.GetExistingSystem<UnloadSystem>().Update(World.World.Unmanaged);

            Assert.IsTrue(World.IsInside(rifle), "a surrendered player's containers stay shut");
        }

        [Test]
        public void SellingAGarrison_LetsPassengersOut()
        {
            var bunker = Bunker(float3.zero);
            var rifle = Passenger(new float3(6f, 0f, 0f), armed: true);
            World.Enter(rifle, bunker);
            World.Run(2f);

            World.Command(1, new PlayerCommand { Type = CommandType.Sell, Unit = bunker });
            World.Tick(frames: 2);

            Assert.IsFalse(World.EntityManager.Exists(bunker));
            Assert.IsFalse(World.IsInside(rifle));
            Assert.AreEqual(Stance.Aggressive, World.Get<CombatStance>(rifle).Value);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ContainerDeath_EjectsOrKillsPassengers_InTheSameFrame(bool survive)
        {
            var bunker = Bunker(float3.zero, survive: survive);
            var rifle = Passenger(new float3(6f, 0f, 0f));
            World.Enter(rifle, bunker);
            World.Run(2f);

            World.Kill(bunker);
            World.Tick();

            Assert.IsFalse(World.EntityManager.Exists(bunker));
            Assert.AreEqual(survive, World.EntityManager.Exists(rifle));
            if (survive)
            {
                Assert.IsFalse(World.IsInside(rifle));
                return;
            }

            var events = World.Buffer<EntityDamaged>(World.Query(typeof(GameEvents)).GetSingletonEntity());
            Assert.AreEqual(rifle, events[0].Entity, "the kill is reported like a lethal hit");
        }
    }
}
