using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Airdrops;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Airdrop flights from the owner's map edge, what their crates pay and what stops them.</summary>
    public class AirdropTests
    {
        private const int Amount = 50;
        private const float Interval = 20f;

        private TestWorld _world;
        private ResourceType _supplies;
        private Entity _receiver;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _supplies = _world.CreateSupplies();

            // The test map spans -100..100: a start at z = -90 enters from the south edge.
            _world.EntityManager.AddComponentData(_world.Player(1), new PlayerSpawn { Position = new float3(60f, 0f, -90f) });
            var aircraft = _world.MakePrefab(_world.SpawnAircraft(0, float3.zero, altitude: 20f, speed: 40f));
            var crate = _world.MakePrefab(_world.EntityManager.CreateEntity(typeof(LocalTransform)));
            _world.EntityManager.SetComponentData(crate, LocalTransform.Identity);
            _receiver = _world.SpawnBuilding(1, new float3(40f, 0f, 0f), new float2(4f, 4f));
            var writer = new EntityManagerWriter(_world.EntityManager, _receiver);
            writer.Add(new AirdropReceiver
            {
                Aircraft = aircraft,
                Crate = crate,
                Type = _supplies,
                Amount = Amount,
                Interval = Interval,
                Altitude = 20f,
                FallSpeed = 20f,
            });
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private Entity[] Flights() => _world.All<AirdropFlight>().ToArray();

        private int Crates() => _world.All<AirdropCrate>().ToArray().Length;

        [Test]
        public void Flight_EntersFromTheEdgeNearestTheOwnersStart_HeadingForTheReceiver()
        {
            _world.Run(Interval + 0.05f);

            var flights = Flights();
            Assert.AreEqual(1, flights.Length);
            var position = _world.Get<LocalTransform>(flights[0]).Position;
            Assert.Less(position.z, -95f, "entered on the south edge");
            Assert.AreEqual(60f, position.x, 2f, "level with the owner's start");
            Assert.AreEqual(20f, position.y, 0.5f, "at cruise altitude");
            Assert.AreEqual(1, _world.Get<Faction>(flights[0]).Value);
            Assert.IsTrue(_world.IsEnabled<MoveDestination>(flights[0]));
            Assert.AreEqual(40f, _world.Get<MoveDestination>(flights[0]).Value.x, 1f, "just past the receiver");
        }

        [Test]
        public void Crate_PaysTheOwnerWhenItLands_AndTheAircraftFliesOffTheMap()
        {
            _world.Run(Interval + 0.05f);

            Assert.IsTrue(_world.RunUntil(() => Crates() > 0, 6f), "dropped over the receiver");
            Assert.AreEqual(0, _world.GetResources(1, _supplies), "nothing until it lands");
            Assert.IsTrue(_world.RunUntil(() => _world.GetResources(1, _supplies) > 0, 3f), "landed");
            Assert.AreEqual(Amount, _world.GetResources(1, _supplies));
            Assert.AreEqual(Amount, _world.Get<PlayerStats>(_world.Player(1)).ResourcesGathered);
            Assert.IsTrue(_world.RunUntil(() => Flights().Length == 0, 8f), "flew off the map and despawned");
        }

        [Test]
        public void LandingPoint_DropsTheCrateThereInsteadOfOnTheOrigin()
        {
            var receiver = _world.Get<AirdropReceiver>(_receiver);
            receiver.Landing = new float3(-3f, 0.5f, 2f);
            _world.EntityManager.SetComponentData(_receiver, receiver);
            _world.Run(Interval + 0.05f);

            Assert.IsTrue(_world.RunUntil(() => Crates() > 0, 6f), "dropped");
            var crate = _world.All<AirdropCrate>().ToArray()[0];
            var position = _world.Get<LocalTransform>(crate).Position;
            Assert.AreEqual(37f, position.x, 1f);
            Assert.AreEqual(2f, position.z, 1f);
            Assert.AreEqual(0.5f, _world.Get<AirdropCrate>(crate).Ground, 0.01f);
        }

        [Test]
        public void DropRun_IsActiveFromJustBeforeTheReleaseUntilJustAfter()
        {
            // Slowed so the run, 60 m either side, starts after the aircraft enters.
            var aircraft = _world.Get<AirdropReceiver>(_receiver).Aircraft;
            _world.EntityManager.SetComponentData(aircraft, new MovementSpeed { Value = 20f });
            _world.Run(Interval + 0.05f);
            var flight = Flights()[0];
            Assert.IsFalse(_world.Get<DropRun>(flight).Active, "closed on the way in");

            Assert.IsTrue(_world.RunUntil(() => _world.Get<DropRun>(flight).Active, 6f));
            Assert.That(DistanceToReceiver(flight), Is.InRange(55f, 61f), "opens about three seconds out");
            Assert.AreEqual(0, Crates());

            Assert.IsTrue(_world.RunUntil(() => Crates() > 0, 4f));
            Assert.IsTrue(_world.Get<DropRun>(flight).Active, "open as the crate leaves");
            Assert.IsTrue(_world.RunUntil(() => !_world.Get<DropRun>(flight).Active, 4f));
            Assert.That(DistanceToReceiver(flight), Is.InRange(59f, 65f), "closes about three seconds after");
        }

        [Test]
        public void ReleasePoint_DropsTheCrateFromIt_OverTheReceiver()
        {
            var aircraft = _world.Get<AirdropReceiver>(_receiver).Aircraft;
            _world.EntityManager.SetComponentData(aircraft, new DropRun { ReleasePoint = new float3(0f, -2f, -6f) });
            _world.Run(Interval + 0.05f);
            var flight = Flights()[0];

            Assert.IsTrue(_world.RunUntil(() => Crates() > 0, 6f));
            var crate = _world.Get<LocalTransform>(_world.All<AirdropCrate>().ToArray()[0]).Position;
            var plane = _world.Get<LocalTransform>(flight).Position;
            Assert.AreEqual(plane.y - 2f, crate.y, 0.5f, "from below the aircraft");
            Assert.Less(math.distance(crate.xz, new float2(40f, 0f)), 1f, "over the receiver");
            Assert.That(DistanceToReceiver(flight), Is.InRange(5f, 8f), "the aircraft is past it by the offset");
        }

        private float DistanceToReceiver(Entity flight) =>
            math.distance(_world.Get<LocalTransform>(flight).Position.xz, new float2(40f, 0f));

        [Test]
        public void ShotDownBeforeTheDrop_LosesTheDelivery()
        {
            _world.Run(Interval + 0.05f);
            _world.EntityManager.SetComponentEnabled<Dead>(Flights()[0], true);

            _world.Run(6f);

            Assert.AreEqual(0, Crates());
            Assert.AreEqual(0, _world.GetResources(1, _supplies));
        }

        [Test]
        public void LowPower_PausesTheTimer()
        {
            _world.Run(Interval * 0.5f);
            _world.SetOffline(_receiver, true);
            _world.Run(Interval);
            Assert.AreEqual(0, Flights().Length, "paused while offline");

            _world.SetOffline(_receiver, false);
            _world.Run(Interval * 0.5f + 0.2f);
            Assert.AreEqual(1, Flights().Length, "resumed where it stopped");
        }

        [Test]
        public void LosingTheReceiver_TurnsTheFlightAwayWithoutADrop()
        {
            _world.Run(Interval + 0.05f);
            _world.EntityManager.DestroyEntity(_receiver);

            Assert.IsTrue(_world.RunUntil(() => Flights().Length == 0, 10f), "flew off the map");
            Assert.AreEqual(0, Crates());
            Assert.AreEqual(0, _world.GetResources(1, _supplies));
        }
    }
}
