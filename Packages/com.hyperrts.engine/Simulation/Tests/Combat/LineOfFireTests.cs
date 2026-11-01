using HyperRTS.Simulation.Combat.Death;
using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Combat
{
    /// <summary>Direct fire needs a clear line: rounds hit what stands in the way, bullets close in, smoke breaks lock.</summary>
    public class LineOfFireTests
    {
        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _world.DisableFog();
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void TankBehindAHouse_CannotFireThroughIt_AndItsRoundHitsTheHouse()
        {
            var tank = _world.SpawnUnit(1, new float3(1f, 0f, 1f));
            _world.AddWeapon(tank, range: 30f, damage: 40f, cooldown: 0.5f, projectile: _world.ProjectilePrefab());
            var house = _world.SpawnBuilding(0, new float3(11f, 0f, 1f), new float2(4f, 4f));
            var writer = new EntityManagerWriter(_world.EntityManager, house);
            SightBlockAuthoring.Add(ref writer, new SightBlock { Size = new float2(4f, 4f), Height = 6f });
            var enemy = _world.SpawnUnit(2, new float3(21f, 0f, 1f), maxHealth: 1000f);
            var houseHealth = _world.GetHealth(house);

            Attack(tank, enemy);
            _world.Run(3f);

            Assert.AreEqual(1000f, _world.GetHealth(enemy), "nothing reaches the target through the house");
            Assert.Less(_world.GetHealth(house), houseHealth, "the rounds hit the house");
        }

        [Test]
        public void ExplicitAttack_WithBullets_MovesUntilTheLineIsClear()
        {
            var rifleman = _world.SpawnUnit(1, new float3(1f, 0f, 1f));
            _world.AddWeapon(rifleman, range: 25f, damage: 10f);
            _world.SpawnSightBlock(new float3(9f, 0f, 1f), new float2(0.5f, 6f), 3f);
            var enemy = _world.SpawnUnit(2, new float3(17f, 0f, 1f), maxHealth: 1000f);

            Attack(rifleman, enemy);
            _world.Run(0.5f);
            Assert.AreEqual(1000f, _world.GetHealth(enemy), "no shot through the wall");
            Assert.Greater(_world.GetPosition(rifleman).x, 1.5f, "it closes in");

            Assert.IsTrue(_world.RunUntil(() => _world.GetHealth(enemy) < 1000f, 10f), "it fires once the line is clear");
        }

        [Test]
        public void Smoke_HidesATarget_BreaksAGuidedRoundsLock_ThenClears()
        {
            using (var map = _world.EntityManager.CreateEntityQuery(typeof(MapSettings)))
            {
                var settings = map.GetSingleton<MapSettings>();
                settings.FogOfWar = true;
                map.SetSingleton(settings);
            }

            var launcher = _world.SpawnUnit(1, new float3(1f, 0f, 1f));
            _world.EntityManager.SetComponentData(launcher, new VisionRange { Value = 40f, EyeHeight = 2f });
            _world.AddWeapon(launcher, range: 30f, cooldown: 100f, projectile: _world.ProjectilePrefab(),
                trajectory: Trajectory.Rocket);
            var smoke = Smoke(new float3(11f, 0f, 1f));
            var enemy = _world.SpawnUnit(2, new float3(21f, 0f, 1f), maxHealth: 1000f);
            _world.EntityManager.AddComponent<TallTarget>(enemy);

            _world.Run(0.3f);
            Assert.IsFalse(_world.Fog().IsVisible(_world.GetPosition(enemy), 1, true), "the smoke hides it");

            Attack(launcher, enemy);
            Assert.IsTrue(_world.RunUntil(() => Projectiles() > 0, 3f), "it fires its missile");
            Assert.AreEqual(Entity.Null, FirstProjectile().Hit.Target, "the smoke broke the lock");

            Assert.IsTrue(_world.RunUntil(() => !_world.EntityManager.IsComponentEnabled<SightBlock>(smoke), 40f),
                "the cloud clears");
        }

        private Entity Smoke(float3 position)
        {
            var smoke = _world.SpawnSightBlock(position,
                new SightBlock { Size = new float2(12f, 12f), Density = 0.5f, PorousTop = 8f, Conceals = true });
            var writer = new EntityManagerWriter(_world.EntityManager, smoke);
            DecayAuthoring.Add(ref writer, "Smoke", 30f, 1f, 0.1f);
            return smoke;
        }

        private void Attack(Entity unit, Entity target) => _world.Command(_world.Get<Faction>(unit).Value,
            new PlayerCommand { Type = CommandType.Attack, Unit = unit, Target = target, Position = _world.GetPosition(target) });

        private int Projectiles()
        {
            using var query = _world.EntityManager.CreateEntityQuery(typeof(Projectile));
            return query.CalculateEntityCount();
        }

        private Projectile FirstProjectile()
        {
            using var query = _world.EntityManager.CreateEntityQuery(typeof(Projectile));
            using var flights = query.ToComponentDataArray<Projectile>(Allocator.Temp);
            return flights[0];
        }
    }
}
