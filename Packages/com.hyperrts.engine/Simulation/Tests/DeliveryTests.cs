using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Resources;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Carrier waves from the map edge to a delivery source, and what each pays.</summary>
    public class DeliveryTests
    {
        private const int Amount = 50;
        private const float Interval = 5f;

        private TestWorld _world;
        private ResourceType _supplies;
        private Entity _source;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _supplies = _world.CreateSupplies();

            // The test map spans -100..100, so carriers enter at x = 100, 60 m east of the source.
            var carrier = _world.MakePrefab(_world.SpawnUnit(0, float3.zero, speed: 20f, name: "Truck"));
            _source = _world.SpawnBuilding(1, new float3(40f, 0f, 0f), new float2(4f, 4f));
            var writer = new EntityManagerWriter(_world.EntityManager, _source);
            writer.Add(new DeliverySource
            {
                Carrier = carrier,
                Count = 2,
                Interval = Interval,
                Type = _supplies,
                Amount = Amount,
                ArrivalRange = 6f,
                Spacing = 4f,
            });
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private Entity[] Carriers() => _world.All<DeliveryCarrier>().ToArray();

        [Test]
        public void Wave_SpawnsAtTheMapEdgeBehindTheSource_HeadingForIt()
        {
            _world.Run(Interval + 0.05f);

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
                Assert.IsTrue(_world.IsEnabled<MoveDestination>(carrier));
                Assert.AreEqual(40f, _world.Get<MoveDestination>(carrier).Value.x, 0.01f);
            }
        }

        [Test]
        public void ArrivingCarriers_PayTheOwner_AndDisappear()
        {
            _world.Run(Interval + 4f);

            Assert.AreEqual(2 * Amount, _world.GetResources(1, _supplies));
            Assert.AreEqual(2 * Amount, _world.Get<PlayerStats>(_world.Player(1)).ResourcesGathered);
            Assert.AreEqual(0, Carriers().Length);
        }

        [Test]
        public void KilledCarriers_PayNothing()
        {
            _world.Run(Interval + 0.05f);
            foreach (var carrier in Carriers())
            {
                _world.EntityManager.SetComponentEnabled<Dead>(carrier, true);
            }

            _world.Run(4f);

            Assert.AreEqual(0, _world.GetResources(1, _supplies));
        }

        [Test]
        public void LosingTheSource_RemovesItsCarriers()
        {
            _world.Run(Interval + 0.05f);
            _world.EntityManager.DestroyEntity(_source);

            _world.Run(0.2f);

            Assert.AreEqual(0, Carriers().Length);
            Assert.AreEqual(0, _world.GetResources(1, _supplies));
        }

        [Test]
        public void OfflineOrNeutralSource_SendsNothing()
        {
            _world.SetOffline(_source, true);
            _world.Run(Interval + 1f);
            Assert.AreEqual(0, Carriers().Length, "offline");

            _world.SetOffline(_source, false);
            _world.EntityManager.SetComponentData(_source, new Faction { Value = Faction.Neutral });
            _world.Run(Interval + 1f);
            Assert.AreEqual(0, Carriers().Length, "neutral");
        }
    }
}
