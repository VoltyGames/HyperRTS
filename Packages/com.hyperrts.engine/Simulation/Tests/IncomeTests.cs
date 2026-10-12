using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Transport;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Passive income while offline, per occupant, from finite reserves with refills, and from casualties.</summary>
    public class IncomeTests
    {
        private TestWorld _world;
        private ResourceType _supplies;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2, 1);
            _supplies = ScriptableObject.CreateInstance<ResourceType>();
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
            Object.DestroyImmediate(_supplies);
        }

        private Entity SpawnBuilding(byte faction, float x) =>
            _world.SpawnBuilding(faction, new float3(x, 0f, 0f), new float2(4f, 4f));

        private Entity SpawnReserve(byte faction, float x, int capacity, int amount = 10, float interval = 1f)
        {
            var derrick = _world.AddIncome(SpawnBuilding(faction, x), _supplies, amount, interval);
            var writer = new EntityManagerWriter(_world.EntityManager, derrick);
            ResourceIncomeSetup.AddReserve(ref writer, capacity);
            return derrick;
        }

        private Entity SpawnRefill(byte faction, float x, int amount)
        {
            var depot = SpawnBuilding(faction, x);
            var writer = new EntityManagerWriter(_world.EntityManager, depot);
            ReserveRefillSetup.Add(ref writer, _supplies, amount, 1f);
            return depot;
        }

        private void SpawnCasualtyIncome(float x)
        {
            var writer = new EntityManagerWriter(_world.EntityManager, SpawnBuilding(1, x));
            CasualtyIncomeSetup.Add(ref writer, _supplies, 0.5f);
        }

        private void Kill(byte faction, int cost)
        {
            var unit = _world.SpawnUnit(faction, new float3(0f, 0f, 20f));
            _world.SetCost(unit, _supplies, cost);
            _world.EntityManager.SetComponentData(unit, new Health { Current = 0f, Max = 100f });
        }

        [Test]
        public void Income_PausesWhileOffline_AndEntitiesWithoutOfflineKeepPaying()
        {
            var derrick = _world.AddIncome(SpawnBuilding(1, 0f), _supplies, 10, 1f);
            _world.AddIncome(_world.SpawnUnit(1, new float3(10f, 0f, 0f)), _supplies, 1, 1f);
            _world.SetOffline(derrick, true);

            _world.Run(2.5f);
            Assert.AreEqual(2, _world.ResourcesOf(1, _supplies), "only the entity without Offline paid");

            _world.SetOffline(derrick, false);
            _world.Run(1f);
            Assert.AreEqual(13, _world.ResourcesOf(1, _supplies), "back online, the paused timer resumes");
            Assert.AreEqual(13, _world.Get<PlayerStats>(_world.Player(1)).ResourcesGathered);
        }

        [Test]
        public void OccupantIncome_PaysPerPassenger_AndNothingWhenEmpty()
        {
            var bunker = _world.AddIncome(SpawnBuilding(1, 0f), _supplies, 5, 1f);
            var writer = new EntityManagerWriter(_world.EntityManager, bunker);
            TransportSetup.AddContainer(ref writer, new Container { Capacity = 4, MaxPassengerSize = 1 });
            ResourceIncomeSetup.AddPerOccupant(ref writer);

            _world.Run(1.5f);
            Assert.AreEqual(0, _world.ResourcesOf(1, _supplies), "empty, it pays nothing");

            for (var i = 0; i < 3; i++)
            {
                var unit = _world.SpawnUnit(1, new float3(10f + i, 0f, 0f));
                _world.EntityManager.GetBuffer<Cargo>(bunker).Add(new Cargo { Unit = unit, Size = 1 });
            }

            _world.Run(1f);
            Assert.AreEqual(15, _world.ResourcesOf(1, _supplies), "one payment for each of three passengers");
        }

        [Test]
        public void Reserve_RunsDry_ThenARefillTopsItUp()
        {
            var derrick = SpawnReserve(1, 0f, 25);

            _world.Run(4.5f);
            Assert.AreEqual(25, _world.ResourcesOf(1, _supplies), "never more than the reserve held");
            Assert.AreEqual(0, _world.Get<ResourceReserve>(derrick).Remaining);
            Assert.IsTrue(_world.EntityManager.Exists(derrick), "a dry reserve stays to be refilled");

            SpawnRefill(1, 10f, 7);
            _world.Run(2f);
            Assert.AreEqual(32, _world.ResourcesOf(1, _supplies), "the refill was paid out again");
        }

        [Test]
        public void Refill_CapsAtCapacity_SkipsOtherOwners_AndPausesWhileOffline()
        {
            var own = SpawnReserve(1, 0f, 20, interval: 1000f);
            var enemy = SpawnReserve(2, 30f, 20, interval: 1000f);
            _world.EntityManager.SetComponentData(own, new ResourceReserve { Remaining = 15, Capacity = 20 });
            _world.EntityManager.SetComponentData(enemy, new ResourceReserve { Remaining = 0, Capacity = 20 });
            var depot = SpawnRefill(1, 10f, 10);

            _world.Run(1.5f);
            Assert.AreEqual(20, _world.Get<ResourceReserve>(own).Remaining, "capped at capacity");
            Assert.AreEqual(0, _world.Get<ResourceReserve>(enemy).Remaining, "another owner's reserve is untouched");

            _world.EntityManager.SetComponentData(own, new ResourceReserve { Remaining = 0, Capacity = 20 });
            _world.SetOffline(depot, true);
            _world.Run(2f);
            Assert.AreEqual(0, _world.Get<ResourceReserve>(own).Remaining, "an offline refill adds nothing");
        }

        [Test]
        public void CasualtyIncome_PaysShareOfOwnAndHostileDeaths_PerEarner()
        {
            SpawnCasualtyIncome(0f);
            SpawnCasualtyIncome(10f);

            Kill(1, 100);
            Kill(2, 40);
            Kill(3, 1000);
            _world.Tick();

            Assert.AreEqual(140, _world.ResourcesOf(1, _supplies), "half of own and enemy costs, twice; allies pay nothing");
            Assert.AreEqual(140, _world.Get<PlayerStats>(_world.Player(1)).ResourcesGathered);
        }

        [Test]
        public void CasualtyIncome_PaysNothingWhileOffline()
        {
            SpawnCasualtyIncome(0f);
            _world.SetOffline(_world.All<CasualtyIncome>()[0], true);
            _world.Tick();

            Kill(2, 100);
            _world.Tick();

            Assert.AreEqual(0, _world.ResourcesOf(1, _supplies));
        }
    }
}
