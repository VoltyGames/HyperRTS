using HyperRTS.Simulation.Combat.Death;
using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Vision;
using HyperRTS.Simulation.Vision.Sight;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Combat
{
    /// <summary>Direct fire needs a clear line: rounds hit what stands in the way, bullets close in, smoke breaks lock.</summary>
    public class LineOfFireTests : SimulationFixture
    {
        [SetUp]
        public void Prepare()
        {
            World.DisableFog();
        }

        [Test]
        public void TankBehindAHouse_CannotFireThroughIt_AndItsRoundHitsTheHouse()
        {
            var tank = World.SpawnUnit(1, new float3(1f, 0f, 1f));
            World.AddWeapon(tank, range: 30f, damage: 40f, cooldown: 0.5f, projectile: World.ProjectilePrefab());
            var house = World.SpawnBuilding(0, new float3(11f, 0f, 1f), new float2(4f, 4f));
            var writer = World.Writer(house);
            SightBlockAuthoring.Add(writer, new SightBlock { Size = new float2(4f, 4f), Height = 6f });
            var enemy = World.SpawnUnit(2, new float3(21f, 0f, 1f), maxHealth: 1000f);
            var houseHealth = World.GetHealth(house);

            Attack(tank, enemy);
            World.Run(3f);

            Assert.AreEqual(1000f, World.GetHealth(enemy), "nothing reaches the target through the house");
            Assert.Less(World.GetHealth(house), houseHealth, "the rounds hit the house");
        }

        [Test]
        public void ExplicitAttack_WithBullets_MovesUntilTheLineIsClear()
        {
            var rifleman = World.SpawnUnit(1, new float3(1f, 0f, 1f));
            World.AddWeapon(rifleman, range: 25f, damage: 10f);
            World.SpawnSightBlock(new float3(9f, 0f, 1f), new float2(0.5f, 6f), 3f);
            var enemy = World.SpawnUnit(2, new float3(17f, 0f, 1f), maxHealth: 1000f);

            Attack(rifleman, enemy);
            World.Run(0.5f);
            Assert.AreEqual(1000f, World.GetHealth(enemy), "no shot through the wall");
            Assert.Greater(World.GetPosition(rifleman).x, 1.5f, "it closes in");

            Assert.IsTrue(World.RunUntil(() => World.GetHealth(enemy) < 1000f, 10f), "it fires once the line is clear");
        }

        [Test]
        public void Smoke_HidesATarget_BreaksAGuidedRoundsLock_ThenClears()
        {
            World.SetFog(true);
            var launcher = World.SpawnViewer(1, new float3(1f, 0f, 1f), 40f);
            World.AddWeapon(launcher, range: 30f, cooldown: 100f, projectile: World.ProjectilePrefab(),
                trajectory: Trajectory.Rocket);
            var smoke = Smoke(new float3(11f, 0f, 1f));
            var enemy = World.SpawnUnit(2, new float3(21f, 0f, 1f), maxHealth: 1000f);
            World.EntityManager.AddComponent<TallTarget>(enemy);

            World.Run(0.3f);
            Assert.IsFalse(World.Fog().IsVisible(World.GetPosition(enemy), 1, true), "the smoke hides it");

            Attack(launcher, enemy);
            Assert.IsTrue(World.RunUntil(() => Projectiles() > 0, 3f), "it fires its missile");
            Assert.AreEqual(Entity.Null, FirstProjectile().Hit.Target, "the smoke broke the lock");

            Assert.IsTrue(World.RunUntil(() => !World.IsEnabled<SightBlock>(smoke), 40f),
                "the cloud clears");
        }

        private Entity Smoke(float3 position)
        {
            var smoke = World.SpawnSightBlock(position,
                new SightBlock { Size = new float2(12f, 12f), Density = 0.5f, PorousTop = 8f, Conceals = true });
            var writer = World.Writer(smoke);
            DecayAuthoring.Add(writer, "Smoke", 30f, 1f, 0.1f);
            return smoke;
        }

        private void Attack(Entity unit, Entity target) => World.Command(World.Get<Faction>(unit).Value,
            new PlayerCommand { Type = CommandType.Attack, Unit = unit, Target = target, Position = World.GetPosition(target) });

        private int Projectiles()
        {
            using var query = World.Query(typeof(Projectile));
            return query.CalculateEntityCount();
        }

        private Projectile FirstProjectile()
        {
            using var query = World.Query(typeof(Projectile));
            using var flights = query.ToComponentDataArray<Projectile>(Allocator.Temp);
            return flights[0];
        }
    }
}
