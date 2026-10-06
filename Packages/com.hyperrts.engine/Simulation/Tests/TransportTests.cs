using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Transport;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Boarding, carrying, firing out, unloading and container death.</summary>
    public class TransportTests
    {
        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private Entity Passenger(float3 position, int size = 1, bool armed = false)
        {
            var unit = _world.SpawnUnit(1, position);
            var sink = new EntityManagerSink(_world.EntityManager, unit);
            TransportSetup.AddPassenger(ref sink, size);
            if (armed)
            {
                _world.Arm(unit, range: 3f, damage: 10f);
            }

            return unit;
        }

        private Entity Bunker(float3 position, int capacity = 2, bool fire = false, bool survive = true)
        {
            var bunker = _world.SpawnBuilding(1, position, new float2(4f, 4f));
            var sink = new EntityManagerSink(_world.EntityManager, bunker);
            TransportSetup.AddContainer(ref sink, new Container
            {
                Capacity = capacity, MaxPassengerSize = 2, PassengersFire = fire, PassengersSurvive = survive,
            });
            return bunker;
        }

        private void Enter(Entity unit, Entity container) =>
            _world.Command(1, new PlayerCommand { Type = CommandType.Smart, Unit = unit, Target = container });

        private bool IsInside(Entity unit) => _world.IsEnabled<Inside>(unit);

        [Test]
        public void SmartClick_Boards_UpToCapacity()
        {
            var bunker = Bunker(float3.zero, capacity: 2);
            var a = Passenger(new float3(8f, 0f, 0f));
            var b = Passenger(new float3(8f, 0f, 2f));
            var c = Passenger(new float3(8f, 0f, -2f));
            Enter(a, bunker);
            Enter(b, bunker);
            Enter(c, bunker);

            _world.Run(4f);

            Assert.AreEqual(2, _world.EntityManager.GetBuffer<Cargo>(bunker).Length);
            Assert.IsTrue(IsInside(a) && IsInside(b));
            Assert.IsFalse(IsInside(c), "no room left");
            Assert.IsFalse(_world.IsEnabled<ActiveOrder>(c), "the order ends when full");
        }

        [Test]
        public void Container_RidingInAnother_TakesNoPassengers()
        {
            var ship = Bunker(float3.zero, capacity: 4);
            var truck = _world.SpawnUnit(1, new float3(5f, 0f, 0f), radius: 1f);
            var sink = new EntityManagerSink(_world.EntityManager, truck);
            TransportSetup.AddContainer(ref sink, new Container { Capacity = 4, MaxPassengerSize = 1 });
            TransportSetup.AddPassenger(ref sink, 2);
            Enter(truck, ship);
            _world.Run(2f);
            Assert.IsTrue(IsInside(truck));

            var rider = Passenger(new float3(8f, 0f, 0f));
            _world.Command(1, new PlayerCommand { Type = CommandType.Enter, Unit = rider, Target = truck });
            _world.Run(2f);

            Assert.IsFalse(IsInside(rider));
            Assert.AreEqual(0, _world.EntityManager.GetBuffer<Cargo>(truck).Length);
        }

        [Test]
        public void Passengers_RideAlong_AndIgnoreOrders()
        {
            var truck = _world.SpawnUnit(1, float3.zero, speed: 5f, radius: 1f);
            var sink = new EntityManagerSink(_world.EntityManager, truck);
            TransportSetup.AddContainer(ref sink, new Container { Capacity = 4, MaxPassengerSize = 1 });
            var rider = Passenger(new float3(4f, 0f, 0f));
            Enter(rider, truck);
            _world.Run(2f);
            Assert.IsTrue(IsInside(rider));

            _world.Command(1, new PlayerCommand { Type = CommandType.Move, Unit = truck, Position = new float3(30f, 0f, 0f) });
            _world.Command(1, new PlayerCommand { Type = CommandType.Move, Unit = rider, Position = new float3(-30f, 0f, 0f) });
            _world.Run(8f);

            var truckPosition = _world.Get<LocalTransform>(truck).Position;
            Assert.Greater(truckPosition.x, 25f);
            Assert.AreEqual(truckPosition.x, _world.Get<LocalTransform>(rider).Position.x, 1e-3f);
        }

        [Test]
        public void Garrison_FiresOut_ButIsNotTargetable()
        {
            var bunker = Bunker(float3.zero, fire: true);
            var rifle = Passenger(new float3(6f, 0f, 0f), armed: true);
            Enter(rifle, bunker);
            _world.Run(2f);

            var enemy = _world.SpawnUnit(2, new float3(0f, 0f, 4.5f), speed: 0f);
            _world.Arm(enemy, Stance.HoldPosition, range: 3f, damage: 10f);
            _world.Run(2f);

            Assert.Less(_world.Get<Health>(enemy).Current, 100f, "fires from the bunker's edge");
            Assert.AreEqual(100f, _world.Get<Health>(rifle).Current, "passengers can't be shot");
            Assert.AreEqual(Stance.HoldPosition, _world.Get<CombatStance>(rifle).Value);
        }

        [Test]
        public void Unload_LetsEveryoneOut_AndRestoresStance()
        {
            var bunker = Bunker(float3.zero);
            var rifle = Passenger(new float3(6f, 0f, 0f), armed: true);
            Enter(rifle, bunker);
            _world.Run(2f);

            _world.Command(1, new PlayerCommand { Type = CommandType.Unload, Unit = bunker, Argument = -1 });
            _world.Tick();

            Assert.IsFalse(IsInside(rifle));
            Assert.AreEqual(0, _world.EntityManager.GetBuffer<Cargo>(bunker).Length);
            Assert.AreEqual(Stance.Aggressive, _world.Get<CombatStance>(rifle).Value);
            Assert.Greater(math.length(_world.Get<LocalTransform>(rifle).Position.xz), 2f, "outside the footprint");
        }

        [Test]
        public void SellingAGarrison_LetsPassengersOut()
        {
            var bunker = Bunker(float3.zero);
            var rifle = Passenger(new float3(6f, 0f, 0f), armed: true);
            Enter(rifle, bunker);
            _world.Run(2f);

            _world.Command(1, new PlayerCommand { Type = CommandType.Sell, Unit = bunker });
            _world.Tick(frames: 2);

            Assert.IsFalse(_world.EntityManager.Exists(bunker));
            Assert.IsFalse(IsInside(rifle));
            Assert.AreEqual(Stance.Aggressive, _world.Get<CombatStance>(rifle).Value);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ContainerDeath_EjectsOrKillsPassengers(bool survive)
        {
            var bunker = Bunker(float3.zero, survive: survive);
            var rifle = Passenger(new float3(6f, 0f, 0f));
            Enter(rifle, bunker);
            _world.Run(2f);

            _world.EntityManager.SetComponentData(bunker, new Health { Current = 0f, Max = 500f });
            _world.Tick(frames: 3);

            Assert.IsFalse(_world.EntityManager.Exists(bunker));
            Assert.AreEqual(survive, _world.EntityManager.Exists(rifle));
            if (survive)
            {
                Assert.IsFalse(IsInside(rifle));
            }
        }
    }
}
