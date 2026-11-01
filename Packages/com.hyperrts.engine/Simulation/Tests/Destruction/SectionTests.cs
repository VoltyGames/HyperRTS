using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Common.Authoring;
using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Destruction;
using HyperRTS.Simulation.Transport;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Simulation.Tests.Destruction
{
    /// <summary>
    /// Sectioned buildings: hits land on the section they reach, materials scale them, what loses its supports falls
    /// and empties its floor, repair rebuilds the latest first, and broken ground-floor walls open paths and leave rubble.
    /// </summary>
    public class SectionTests
    {
        private const int Front = 0;
        private const int Back = 1;
        private const int Right = 3;
        private const int Roof = 4;
        private const int Upper = 5;
        private const byte RubbleCrush = 3;

        private TestWorld _world;
        private DamageType _highExplosive;
        private DamageType _smallArms;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _highExplosive = _world.Own(ScriptableObject.CreateInstance<DamageType>());
            _smallArms = _world.Own(ScriptableObject.CreateInstance<DamageType>());
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private static Section Wall(float3 center, float3 half, int floor = 0, ulong supports = 0) => new()
        {
            Center = center, HalfSize = half, Health = 100f, MaxHealth = 100f, Floor = (byte)floor,
            Anchored = floor == 0, Closes = floor == 0, Exterior = true, Supports = supports, Outward = math.normalizesafe(center * new float3(1f, 0f, 1f)),
        };

        /// <summary>
        /// An 8 m square house: front (+Z), back, left and right brick walls, a roof on all four, and with
        /// <paramref name="upper"/> a first-floor wall standing on the front wall alone.
        /// </summary>
        private Entity House(float3 position, bool upper = false, Entity rubble = default)
        {
            var sections = new List<Section>
            {
                Wall(new float3(0f, 1.5f, 4f), new float3(4f, 1.5f, 0.15f)),
                Wall(new float3(0f, 1.5f, -4f), new float3(4f, 1.5f, 0.15f)),
                Wall(new float3(-4f, 1.5f, 0f), new float3(0.15f, 1.5f, 4f)),
                Wall(new float3(4f, 1.5f, 0f), new float3(0.15f, 1.5f, 4f)),
                new()
                {
                    Center = new float3(0f, 3.2f, 0f), HalfSize = new float3(4f, 0.2f, 4f), Health = 100f,
                    MaxHealth = 100f, Supports = 0b1111, Outward = math.up(),
                },
            };
            if (upper)
            {
                sections.Add(Wall(new float3(0f, 4.5f, 4f), new float3(4f, 1.5f, 0.15f), 1, 1ul << Front));
            }

            var factors = new List<SectionFactor>
            {
                new() { Material = 0, DamageType = _highExplosive, Multiplier = 1.5f },
                new() { Material = 0, DamageType = _smallArms, Multiplier = 0.25f },
            };
            var house = _world.SpawnBuilding(1, position, new float2(8.3f, 8.3f));
            var writer = new EntityManagerWriter(_world.EntityManager, house);
            SectionsSetup.Add(ref writer, sections, factors, rubble);
            var health = _world.Get<Health>(house);
            health.Max = health.Current = 100f * sections.Count;
            _world.EntityManager.SetComponentData(house, health);
            return house;
        }

        private Section SectionOf(Entity house, int index) => _world.EntityManager.GetBuffer<Section>(house)[index];

        private bool IsBroken(Entity house, int index) =>
            (_world.Get<SectionedBuilding>(house).Broken & (1ul << index)) != 0;

        private void Shoot(Entity house, float3 from, float3 at, float amount, DamageType type = null, float radius = 0f) =>
            _world.QueueDamage(new DamageEvent
            {
                Target = house, Origin = from, Position = at, SourceFaction = 2, Amount = amount, Type = type,
                Radius = radius, EdgeFactor = 0.5f,
            });

        [Test]
        public void DirectHit_DamagesTheSectionItsLineEnters_NotTheOneBehind()
        {
            var house = House(float3.zero);
            _world.Tick();

            Shoot(house, new float3(0f, 1.5f, 30f), new float3(0f, 1.5f, 0f), 30f);
            _world.Tick();

            Assert.AreEqual(70f, SectionOf(house, Front).Health, 1e-3f);
            Assert.AreEqual(100f, SectionOf(house, Back).Health);
            Assert.AreEqual(470f, _world.GetHealth(house), 1e-3f, "building health is the sum of its sections");
        }

        [Test]
        public void Splash_ReachesSectionsWithinItsRadius_WithFalloff()
        {
            var house = House(float3.zero);
            _world.Tick();

            var centre = new float3(4.5f, 1.5f, 0f);
            _world.QueueDamage(new DamageEvent
            {
                Position = centre, Origin = new float3(30f, 1.5f, 0f), SourceFaction = 2, Amount = 30f, Radius = 3f,
                EdgeFactor = 0.5f,
            });
            _world.Tick();

            var right = SectionOf(house, Right);
            var expected = 30f * new DamageEvent { Radius = 3f, EdgeFactor = 0.5f }.SplashFactor(right.Gap(centre));
            Assert.AreEqual(100f - expected, right.Health, 1e-3f);
            Assert.Less(SectionOf(house, Roof).Health, 100f, "the roof's edge is in reach");
            Assert.Greater(SectionOf(house, Roof).Health, right.Health, "and farther, so it takes less");
            Assert.AreEqual(100f, SectionOf(house, Front).Health, "out of reach");
        }

        [Test]
        public void Brick_LosesMoreToHighExplosiveThanToSmallArms()
        {
            var shelled = House(float3.zero);
            var shot = House(new float3(30f, 0f, 0f));
            _world.Tick();

            Shoot(shelled, new float3(0f, 1.5f, 30f), new float3(0f, 1.5f, 0f), 20f, _highExplosive);
            Shoot(shot, new float3(30f, 1.5f, 30f), new float3(30f, 1.5f, 0f), 20f, _smallArms);
            _world.Tick();

            Assert.AreEqual(70f, SectionOf(shelled, Front).Health, 1e-3f);
            Assert.AreEqual(95f, SectionOf(shot, Front).Health, 1e-3f);
        }

        [Test]
        public void SectionCutOffFromTheGround_FallsAfterTheDelay_AndEmptiesItsFloor()
        {
            var house = House(float3.zero, upper: true);
            var writer = new EntityManagerWriter(_world.EntityManager, house);
            TransportSetup.AddContainer(ref writer, new Container { Capacity = 4, MaxPassengerSize = 1 });
            var squads = new Entity[4];
            for (var i = 0; i < squads.Length; i++)
            {
                squads[i] = _world.SpawnUnit(1, new float3(20f + i * 2f, 0f, 0f));
                var passenger = new EntityManagerWriter(_world.EntityManager, squads[i]);
                TransportSetup.AddPassenger(ref passenger, 1);
                _world.EntityManager.GetBuffer<Cargo>(house).Add(new Cargo { Unit = squads[i], Size = 1 });
                _world.EntityManager.SetComponentData(squads[i], new Inside { Container = house });
                _world.EntityManager.SetComponentEnabled<Inside>(squads[i], true);
            }

            _world.Tick();

            Shoot(house, new float3(0f, 1.5f, 30f), new float3(0f, 1.5f, 0f), 200f);
            _world.Tick();
            Assert.IsTrue(IsBroken(house, Front));
            Assert.IsFalse(IsBroken(house, Upper), "it waits a support level's delay");

            _world.Run(SectionedBuilding.FallDelay + 0.2f);

            Assert.IsTrue(IsBroken(house, Upper));
            Assert.IsFalse(IsBroken(house, Roof), "the other walls still hold the roof");
            Assert.IsTrue(_world.IsEnabled<Inside>(squads[0]) && _world.IsEnabled<Inside>(squads[1]), "ground floor stays");
            for (var i = 2; i < 4; i++)
            {
                Assert.IsFalse(_world.IsEnabled<Inside>(squads[i]), "the first floor's passengers get out");
                Assert.AreEqual(100f * (1f - FallenFloorSystem.FallHit), _world.GetHealth(squads[i]), 1e-3f);
            }

            Assert.AreEqual(2, _world.Get<Container>(house).LostSlots(_world.Get<IntactFloors>(house)), "and its slots close");
        }

        [Test]
        public void Repair_RebuildsTheLatestBrokenSectionFirst()
        {
            var house = House(float3.zero);
            _world.Tick();

            Shoot(house, new float3(0f, 1.5f, 30f), new float3(0f, 1.5f, 0f), 100f);
            _world.Tick();
            Shoot(house, new float3(30f, 1.5f, 0f), new float3(0f, 1.5f, 0f), 100f);
            _world.Tick();
            Assert.AreEqual(300f, _world.GetHealth(house), 1e-3f);

            _world.QueueDamage(new DamageEvent { Target = house, Amount = -110f });
            _world.Tick();

            Assert.IsFalse(IsBroken(house, Right), "the latest comes back first");
            Assert.IsTrue(IsBroken(house, Front), "the earlier stays down until it is full");
            Assert.AreEqual(10f, SectionOf(house, Front).Health, 1e-3f);
            Assert.AreEqual(410f, _world.GetHealth(house), 1e-3f);
        }

        [Test]
        public void FallenGroundFloorWall_OpensItsCells_AndLeavesRubble()
        {
            var rubble = _world.MakePrefab(_world.EntityManager.CreateEntity());
            _world.EntityManager.AddComponentData(rubble, LocalTransform.Identity);
            _world.EntityManager.AddComponentData(rubble, new Destructible { Crush = RubbleCrush });
            var house = House(new float3(0.5f, 0f, 0.5f), rubble: rubble);
            _world.Tick(frames: 2);
            var wall = new float3(0.5f, 0f, 4.5f);
            Assert.IsFalse(_world.Grid().IsWalkable(wall), "a standing wall closes its cells");

            Shoot(house, new float3(0.5f, 1.5f, 30f), new float3(0.5f, 1.5f, 0.5f), 200f);
            _world.Tick(frames: 3);

            var grid = _world.Grid();
            Assert.IsTrue(grid.IsWalkable(wall), "the breach opens");
            Assert.AreEqual(RubbleCrush, grid.Scenery[grid.Index(grid.WorldToCell(wall))], "rubble lies in it");
            using var heaps = _world.EntityManager.CreateEntityQuery(typeof(SceneryId), typeof(Destructible))
                .ToEntityArray(Allocator.Temp);
            Assert.AreEqual(1, heaps.Length, "one heap, at the broken wall's foot");
        }
    }
}
