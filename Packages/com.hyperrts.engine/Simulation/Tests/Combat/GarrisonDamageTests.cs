using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Transport;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Tests.Combat
{
    /// <summary>Hits on a sheltering building reach its garrison past the walls, by damage type and building health.</summary>
    public class GarrisonDamageTests
    {
        private const float Protection = 0.6f;
        private const float Shot = 20f;

        private TestWorld _world;
        private DamageType _smallArms;
        private DamageType _highExplosive;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _smallArms = Type(0.5f);
            _highExplosive = Type(1.3f);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private DamageType Type(float garrisonFactor)
        {
            var type = _world.Own(ScriptableObject.CreateInstance<DamageType>());
            type.garrisonFactor = garrisonFactor;
            return type;
        }

        private Entity House(float3 position)
        {
            var house = _world.SpawnBuilding(1, position, new float2(8f, 8f));
            var writer = new EntityManagerWriter(_world.EntityManager, house);
            TransportSetup.AddContainer(ref writer, new Container { Capacity = 5, MaxPassengerSize = 1 });
            TransportSetup.AddShelter(ref writer, Protection);
            return house;
        }

        private Entity Squad(float3 position)
        {
            var squad = _world.SpawnUnit(1, position);
            var writer = new EntityManagerWriter(_world.EntityManager, squad);
            TransportSetup.AddPassenger(ref writer, 1);
            return squad;
        }

        /// <summary>Puts <paramref name="squad"/> straight into <paramref name="house"/>, as boarding ends.</summary>
        private void Garrison(Entity squad, Entity house)
        {
            _world.EntityManager.GetBuffer<Cargo>(house).Add(new Cargo { Unit = squad, Size = 1 });
            _world.EntityManager.SetComponentData(squad, new Inside { Container = house });
            _world.EntityManager.SetComponentEnabled<Inside>(squad, true);
        }

        private void Hit(Entity target, DamageType type, float amount = Shot) =>
            _world.QueueDamage(new DamageEvent
            {
                Target = target,
                Origin = new float3(30f, 0f, 0f),
                SourceFaction = 2,
                Amount = amount,
                Type = type,
                GarrisonFactor = DamageType.Garrison(type),
            });

        /// <summary>One frame resolves the hit on the house, the next its passengers' shares.</summary>
        private void Resolve() => _world.Tick(frames: 3);

        private float Lost(Entity unit) => 100f - _world.GetHealth(unit);

        [Test]
        public void GarrisonedSquad_LosesLessThanInTheOpen()
        {
            var house = House(float3.zero);
            var inside = Squad(new float3(10f, 0f, 0f));
            var outside = Squad(new float3(40f, 0f, 0f));
            Garrison(inside, house);
            _world.Tick();

            Hit(house, _smallArms);
            Hit(outside, _smallArms);
            Resolve();

            Assert.AreEqual(Shot * (1f - Protection) * 0.5f, Lost(inside), 1e-3f);
            Assert.AreEqual(Shot, Lost(outside), 1e-3f);
        }

        [Test]
        public void HighExplosive_HurtsTheGarrisonMoreThanSmallArms()
        {
            var shot = House(float3.zero);
            var shelled = House(new float3(30f, 0f, 0f));
            var rifles = Squad(new float3(10f, 0f, 0f));
            var shells = Squad(new float3(40f, 0f, 0f));
            Garrison(rifles, shot);
            Garrison(shells, shelled);
            _world.Tick();

            Hit(shot, _smallArms);
            Hit(shelled, _highExplosive);
            Resolve();

            Assert.Greater(Lost(shells), Lost(rifles));
            Assert.AreEqual(Shot * (1f - Protection) * 1.3f, Lost(shells), 1e-3f);
        }

        [Test]
        public void DamagedBuilding_ProtectsLess()
        {
            var house = House(float3.zero);
            var squad = Squad(new float3(10f, 0f, 0f));
            Garrison(squad, house);
            var health = _world.Get<Health>(house);
            health.Current = health.Max * 0.5f;
            _world.EntityManager.SetComponentData(house, health);
            _world.Tick();

            Hit(house, _smallArms);
            Resolve();

            Assert.AreEqual(Shot * (1f - Protection * 0.5f) * 0.5f, Lost(squad), 1e-3f);
        }

        [Test]
        public void Hit_IsSharedAmongThePassengers()
        {
            var house = House(float3.zero);
            var first = Squad(new float3(10f, 0f, 0f));
            var second = Squad(new float3(12f, 0f, 0f));
            Garrison(first, house);
            Garrison(second, house);
            _world.Tick();

            Hit(house, _highExplosive);
            Resolve();

            var share = Shot * (1f - Protection) * 1.3f / 2f;
            Assert.AreEqual(share, Lost(first), 1e-3f);
            Assert.AreEqual(share, Lost(second), 1e-3f);
        }

        [Test]
        public void PassengerKilledInside_FreesItsSlot()
        {
            var house = House(float3.zero);
            var squad = Squad(new float3(10f, 0f, 0f));
            Garrison(squad, house);
            _world.Tick();

            Hit(house, _highExplosive, 400f);
            Resolve();

            Assert.IsEmpty(_world.EntityManager.GetBuffer<Cargo>(house).AsNativeArray().ToArray());
        }
    }
}
