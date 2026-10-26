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

namespace HyperRTS.Simulation.Tests.Economy
{
    /// <summary>Supplies that arrive from the map edge: carrier waves to a delivery source and airdrop flights.</summary>
    public class DeliveryTests
    {
        private const int Amount = 50;
        private const float CarrierInterval = 5f;
        private const float AirdropInterval = 20f;

        private TestWorld _world;
        private ResourceType _supplies;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _supplies = _world.CreateSupplies();
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        /// <summary>A source 60 m west of the east edge, as the test map spans -100 to 100.</summary>
        private Entity DeliverySource()
        {
            var carrier = _world.MakePrefab(_world.SpawnUnit(0, float3.zero, speed: 20f, name: "Truck"));
            var source = _world.SpawnBuilding(1, new float3(40f, 0f, 0f), new float2(4f, 4f));
            var writer = new EntityManagerWriter(_world.EntityManager, source);
            writer.Add(new DeliverySource
            {
                Carrier = carrier, Count = 2, Interval = CarrierInterval, Type = _supplies, Amount = Amount,
                ArrivalRange = 6f, Spacing = 4f,
            });
            return source;
        }

        /// <summary>A receiver at (40, 0) for a player starting at z = -90, so flights enter from the south edge.</summary>
        private Entity AirdropReceiver()
        {
            _world.EntityManager.AddComponentData(_world.Player(1), new PlayerSpawn { Position = new float3(60f, 0f, -90f) });
            var aircraft = _world.MakePrefab(_world.SpawnAircraft(0, float3.zero, altitude: 20f, speed: 40f));
            var crate = _world.MakePrefab(_world.EntityManager.CreateEntity(typeof(LocalTransform)));
            _world.EntityManager.SetComponentData(crate, LocalTransform.Identity);
            var receiver = _world.SpawnBuilding(1, new float3(40f, 0f, 0f), new float2(4f, 4f));
            var writer = new EntityManagerWriter(_world.EntityManager, receiver);
            writer.Add(new AirdropReceiver
            {
                Aircraft = aircraft, Crate = crate, Type = _supplies, Amount = Amount, Interval = AirdropInterval,
                Altitude = 20f, FallSpeed = 20f,
            });
            return receiver;
        }

        private Entity[] Carriers() => _world.All<DeliveryCarrier>().ToArray();

        private Entity[] Flights() => _world.All<AirdropFlight>().ToArray();

        private Entity[] Crates() => _world.All<AirdropCrate>().ToArray();

        private float DistanceToReceiver(Entity flight) =>
            math.distance(_world.Get<LocalTransform>(flight).Position.xz, new float2(40f, 0f));

        [Test]
        public void Wave_SpawnsAtTheMapEdgeBehindTheSource_HeadingForIt()
        {
            DeliverySource();
            _world.Run(CarrierInterval + 0.05f);

            var carriers = Carriers();
            Assert.AreEqual(2, carriers.Length);
            var first = _world.Get<LocalTransform>(carriers[0]).Position;
            var second = _world.Get<LocalTransform>(carriers[1]).Position;
            Assert.Greater(first.x, 95f, "entered on the east edge");
            // Avoidance reads last frame's velocities, so a fresh pair drifts a little on its first frame.
            Assert.AreEqual(4f, math.abs(first.z - second.z), 0.6f, "spread sideways, not stacked");
            Assert.AreEqual(0, _world.Get<Population>(_world.Player(1)).Used, "carriers use no population");
            foreach (var carrier in carriers)
            {
                Assert.AreEqual(1, _world.Get<Faction>(carrier).Value);
                Assert.AreEqual(40f, _world.Get<MoveDestination>(carrier).Value.x, 0.01f);
            }
        }

        [Test]
        public void ArrivingCarriers_PayTheOwner_AndDisappear_ButKilledOnesPayNothing()
        {
            DeliverySource();
            _world.Run(CarrierInterval + 4f);
            Assert.AreEqual(2 * Amount, _world.GetResources(1, _supplies));
            Assert.AreEqual(2 * Amount, _world.Get<PlayerStats>(_world.Player(1)).ResourcesGathered);
            Assert.AreEqual(0, Carriers().Length);

            _world.Run(CarrierInterval - 4f + 0.05f);
            foreach (var carrier in Carriers())
            {
                _world.EntityManager.SetComponentEnabled<Dead>(carrier, true);
            }

            _world.Run(4f);
            Assert.AreEqual(2 * Amount, _world.GetResources(1, _supplies), "the second wave was killed");
        }

        [Test]
        public void LosingTheSource_RemovesItsCarriers()
        {
            var source = DeliverySource();
            _world.Run(CarrierInterval + 0.05f);
            _world.EntityManager.DestroyEntity(source);

            _world.Run(0.2f);

            Assert.AreEqual(0, Carriers().Length);
            Assert.AreEqual(0, _world.GetResources(1, _supplies));
        }

        [Test]
        public void OfflineOrNeutralSource_SendsNothing()
        {
            var source = DeliverySource();
            _world.SetOffline(source, true);
            _world.Run(CarrierInterval + 1f);
            Assert.AreEqual(0, Carriers().Length, "offline");

            _world.SetOffline(source, false);
            _world.EntityManager.SetComponentData(source, new Faction { Value = Faction.Neutral });
            _world.Run(CarrierInterval + 1f);
            Assert.AreEqual(0, Carriers().Length, "neutral");
        }

        [Test]
        public void Flight_EntersFromTheEdgeNearestTheOwnersStart_AndItsCratePaysOnLanding()
        {
            AirdropReceiver();
            _world.Run(AirdropInterval + 0.05f);

            var flights = Flights();
            Assert.AreEqual(1, flights.Length);
            var position = _world.Get<LocalTransform>(flights[0]).Position;
            Assert.Less(position.z, -95f, "entered on the south edge");
            Assert.AreEqual(60f, position.x, 2f, "level with the owner's start");
            Assert.AreEqual(20f, position.y, 0.5f, "at cruise altitude");
            Assert.AreEqual(1, _world.Get<Faction>(flights[0]).Value);
            Assert.AreEqual(40f, _world.Get<MoveDestination>(flights[0]).Value.x, 1f, "just past the receiver");

            Assert.IsTrue(_world.RunUntil(() => Crates().Length > 0, 6f), "dropped over the receiver");
            Assert.AreEqual(0, _world.GetResources(1, _supplies), "nothing until it lands");
            Assert.IsTrue(_world.RunUntil(() => _world.GetResources(1, _supplies) > 0, 3f), "landed");
            Assert.AreEqual(Amount, _world.GetResources(1, _supplies));
            Assert.AreEqual(Amount, _world.Get<PlayerStats>(_world.Player(1)).ResourcesGathered);
            Assert.IsTrue(_world.RunUntil(() => Flights().Length == 0, 8f), "flew off the map and despawned");
        }

        [Test]
        public void LandingPoint_DropsTheCrateThereInsteadOfOnTheOrigin()
        {
            var receiver = AirdropReceiver();
            var settings = _world.Get<AirdropReceiver>(receiver);
            settings.Landing = new float3(-3f, 0.5f, 2f);
            _world.EntityManager.SetComponentData(receiver, settings);
            _world.Run(AirdropInterval + 0.05f);

            Assert.IsTrue(_world.RunUntil(() => Crates().Length > 0, 6f), "dropped");
            var crate = Crates()[0];
            var position = _world.Get<LocalTransform>(crate).Position;
            Assert.AreEqual(37f, position.x, 1f);
            Assert.AreEqual(2f, position.z, 1f);
            Assert.AreEqual(0.5f, _world.Get<AirdropCrate>(crate).Ground, 0.01f);
        }

        [Test]
        public void DropRun_IsActiveFromJustBeforeTheReleaseUntilJustAfter()
        {
            // Slowed so the run, 60 m either side, starts after the aircraft enters.
            var aircraft = _world.Get<AirdropReceiver>(AirdropReceiver()).Aircraft;
            _world.EntityManager.SetComponentData(aircraft, new MovementSpeed { Value = 20f });
            _world.Run(AirdropInterval + 0.05f);
            var flight = Flights()[0];
            Assert.IsFalse(_world.Get<DropRun>(flight).Active, "closed on the way in");

            Assert.IsTrue(_world.RunUntil(() => _world.Get<DropRun>(flight).Active, 6f));
            Assert.That(DistanceToReceiver(flight), Is.InRange(55f, 61f), "opens about three seconds out");
            Assert.AreEqual(0, Crates().Length);

            Assert.IsTrue(_world.RunUntil(() => Crates().Length > 0, 4f));
            Assert.IsTrue(_world.Get<DropRun>(flight).Active, "open as the crate leaves");
            Assert.IsTrue(_world.RunUntil(() => !_world.Get<DropRun>(flight).Active, 4f));
            Assert.That(DistanceToReceiver(flight), Is.InRange(59f, 65f), "closes about three seconds after");
        }

        [Test]
        public void ReleasePoint_DropsTheCrateFromIt_OverTheReceiver()
        {
            var aircraft = _world.Get<AirdropReceiver>(AirdropReceiver()).Aircraft;
            _world.EntityManager.SetComponentData(aircraft, new DropRun { ReleasePoint = new float3(0f, -2f, -6f) });
            _world.Run(AirdropInterval + 0.05f);
            var flight = Flights()[0];

            Assert.IsTrue(_world.RunUntil(() => Crates().Length > 0, 6f));
            var crate = _world.Get<LocalTransform>(Crates()[0]).Position;
            var plane = _world.Get<LocalTransform>(flight).Position;
            Assert.AreEqual(plane.y - 2f, crate.y, 0.5f, "from below the aircraft");
            Assert.Less(math.distance(crate.xz, new float2(40f, 0f)), 1f, "over the receiver");
            Assert.That(DistanceToReceiver(flight), Is.InRange(5f, 8f), "the aircraft is past it by the offset");
        }

        [Test]
        public void DropFlight_ReleasesOnceOverTheDropPoint_ThenHeadsForTheExit()
        {
            var flight = new DropFlight
            {
                Route = new DropRoute
                {
                    Entry = new float3(0f, 20f, -50f), Target = float3.zero, Exit = new float3(0f, 20f, 50f),
                },
            };
            var run = new DropRun();
            var destination = new MoveDestination();

            Assert.IsFalse(flight.Fly(LocalTransform.FromPosition(0f, 20f, -10f), 10f, ref run, ref destination, out _));
            Assert.IsFalse(flight.Leaving, "short of it");
            Assert.IsTrue(run.Active, "within three seconds");
            Assert.IsTrue(flight.Fly(LocalTransform.FromPosition(0f, 20f, 1f), 10f, ref run, ref destination,
                out var release));
            Assert.AreEqual(new float3(0f, 20f, 1f), release);
            Assert.AreEqual(flight.Route.Exit, destination.Value);
            Assert.IsFalse(flight.Fly(LocalTransform.FromPosition(0f, 20f, 2f), 10f, ref run, ref destination, out _),
                "only once");
        }

        [Test]
        public void ShotDownBeforeTheDrop_LosesTheDelivery()
        {
            AirdropReceiver();
            _world.Run(AirdropInterval + 0.05f);
            _world.EntityManager.SetComponentEnabled<Dead>(Flights()[0], true);

            _world.Run(6f);

            Assert.AreEqual(0, Crates().Length);
            Assert.AreEqual(0, _world.GetResources(1, _supplies));
        }

        [Test]
        public void LosingTheReceiver_TurnsTheFlightAwayWithoutADrop()
        {
            var receiver = AirdropReceiver();
            _world.Run(AirdropInterval + 0.05f);
            _world.EntityManager.DestroyEntity(receiver);

            Assert.IsTrue(_world.RunUntil(() => Flights().Length == 0, 10f), "flew off the map");
            Assert.AreEqual(0, Crates().Length);
            Assert.AreEqual(0, _world.GetResources(1, _supplies));
        }

        [Test]
        public void LowPower_PausesTheAirdropTimer()
        {
            var receiver = AirdropReceiver();
            _world.Run(AirdropInterval * 0.5f);
            _world.SetOffline(receiver, true);
            _world.Run(AirdropInterval);
            Assert.AreEqual(0, Flights().Length, "paused while offline");

            _world.SetOffline(receiver, false);
            _world.Run(AirdropInterval * 0.5f + 0.2f);
            Assert.AreEqual(1, Flights().Length, "resumed where it stopped");
        }
    }
}
