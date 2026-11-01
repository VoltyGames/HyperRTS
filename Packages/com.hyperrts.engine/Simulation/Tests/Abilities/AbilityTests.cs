using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Abilities
{
    /// <summary>Unit abilities, building superweapons and player support powers.</summary>
    public class AbilityTests : SimulationFixture
    {
        private static readonly int Grenade = EntityInfo.TypeIdFromName("Grenade");

        private static Ability Spec(string name, AbilityTarget target, float range, float cooldown, float damage = 0f,
            float radius = 0f) => new()
        {
            Id = EntityInfo.TypeIdFromName(name),
            Name = name,
            Target = target,
            Filter = AbilityTargetFilter.Hostile,
            Range = range,
            Cooldown = cooldown,
            Damage = damage,
            Radius = radius,
        };

        private Entity Give(Entity entity, Ability ability)
        {
            if (!World.EntityManager.HasBuffer<Ability>(entity))
            {
                World.EntityManager.AddBuffer<Ability>(entity);
            }

            World.Buffer<Ability>(entity).Add(ability);
            return entity;
        }

        private Ability GetAbility(Entity entity) => World.Buffer<Ability>(entity)[0];

        private DynamicBuffer<AbilityActivation> Events()
        {
            using var query = World.Query(typeof(AbilityEvents));
            return World.EntityManager.GetBuffer<AbilityActivation>(query.GetSingletonEntity());
        }

        [Test]
        public void UnitAbility_WalksIntoRange_FiresSplash_ThenCoolsDown()
        {
            var soldier = Give(World.SpawnUnit(1, float3.zero), Spec("Grenade", AbilityTarget.Point, 5f, 20f, 50f, 3f));
            var a = World.SpawnUnit(2, new float3(15f, 0f, 0f), speed: 0f);
            var b = World.SpawnUnit(2, new float3(15f, 0f, 2f), speed: 0f);
            var command = new PlayerCommand
            {
                Type = CommandType.UseAbility, Unit = soldier, Position = new float3(15f, 0f, 0f), Argument = Grenade,
            };
            World.Command(1, command);

            World.Run(4f);

            Assert.AreEqual(50f, World.Get<Health>(a).Current, 1e-3f);
            Assert.AreEqual(50f, World.Get<Health>(b).Current, 1e-3f, "splash");
            Assert.Less(World.Get<LocalTransform>(soldier).Position.x, 11f, "stopped once in range");
            Assert.Greater(GetAbility(soldier).CooldownRemaining, 10f);
            Assert.IsFalse(World.IsEnabled<ActiveOrder>(soldier));

            World.Command(1, command);
            World.Run(1f);
            Assert.AreEqual(50f, World.Get<Health>(a).Current, 1e-3f, "still cooling down");
        }

        [Test]
        public void BuildingAbility_ChargesUp_ThenSpawnsAtTarget()
        {
            var beacon = World.MakePrefab(World.EntityManager.CreateEntity(typeof(LocalTransform)));
            var strike = Spec("Strike", AbilityTarget.Point, 0f, 2f);
            strike.SpawnPrefab = beacon;
            strike.CooldownRemaining = 2f;
            var silo = Give(World.SpawnBuilding(1, float3.zero, new float2(4f, 4f)), strike);
            var command = new PlayerCommand
            {
                Type = CommandType.UseAbility, Unit = silo, Position = new float3(60f, 0f, 60f), Argument = strike.Id,
            };

            World.Command(1, command);
            World.Tick();
            Assert.AreEqual(0, Events().Length, "charging");

            World.Run(2.1f);
            World.Command(1, command);
            World.Tick();

            Assert.AreEqual(1, Events().Length);
            Assert.AreEqual(strike.Id, Events()[0].AbilityId);
            using var beacons = World.Query(
                ComponentType.ReadOnly<LocalTransform>(), ComponentType.Exclude<EntityInfo>(),
                ComponentType.ReadOnly<Faction>());
            using var positions = beacons.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            Assert.AreEqual(1, positions.Length);
            Assert.AreEqual(60f, positions[0].Position.x, 1e-3f);
        }

        [Test]
        public void PlayerPower_NeedsItsBuilding()
        {
            var power = Spec("Airstrike", AbilityTarget.Point, 0f, 60f, 30f, 5f);
            power.RequiredTypeId = EntityInfo.TypeIdFromName("Airfield");
            Give(World.Player(1), power);
            var enemy = World.SpawnUnit(2, new float3(40f, 0f, 40f), speed: 0f);
            var command = new PlayerCommand
            {
                Type = CommandType.UsePower, Position = new float3(40f, 0f, 40f), Argument = power.Id,
            };

            World.Command(1, command);
            World.Tick();
            Assert.AreEqual(100f, World.Get<Health>(enemy).Current, "no airfield yet");

            World.SpawnBuilding(1, new float3(-40f, 0f, 0f), new float2(4f, 4f), name: "Airfield");
            World.Command(1, command);
            World.Tick();

            Assert.AreEqual(70f, World.Get<Health>(enemy).Current, 1e-3f);
            Assert.AreEqual(60f, GetAbility(World.Player(1)).CooldownRemaining, 0.1f);
        }

        [Test]
        public void EntityAbility_IgnoresCloakedAndGarrisonedEnemies()
        {
            var snipe = Spec("Snipe", AbilityTarget.Entity, 0f, 30f, 40f);
            Give(World.Player(1), snipe);
            var cloaked = World.SpawnUnit(2, new float3(20f, 0f, 0f), speed: 0f);
            var writer = World.Writer(cloaked);
            StealthSetup.AddStealth(writer, new Stealth { RevealDuration = 1f }, true);
            var bunker = World.SpawnBuilding(2, new float3(-20f, 0f, 0f), new float2(4f, 4f));
            var garrisoned = World.SpawnUnit(2, new float3(-20f, 0f, 0f), speed: 0f);
            World.Add(garrisoned, new Inside { Container = bunker });
            var visible = World.SpawnUnit(2, new float3(0f, 0f, 20f), speed: 0f);

            foreach (var target in new[] { cloaked, garrisoned })
            {
                World.Command(1, new PlayerCommand
                {
                    Type = CommandType.UsePower, Target = target, Argument = snipe.Id,
                });
                World.Tick();
                Assert.AreEqual(0f, GetAbility(World.Player(1)).CooldownRemaining, "never fired");
            }

            World.Command(1, new PlayerCommand { Type = CommandType.UsePower, Target = visible, Argument = snipe.Id });
            World.Tick();
            Assert.AreEqual(60f, World.Get<Health>(visible).Current, 1e-3f);
        }

        [Test]
        public void UnitAbility_LosingItsBuildingMidWalk_CancelsTheCast()
        {
            var grenade = Spec("Grenade", AbilityTarget.Point, 5f, 20f, 50f, 3f);
            grenade.RequiredTypeId = EntityInfo.TypeIdFromName("Armory");
            var soldier = Give(World.SpawnUnit(1, float3.zero), grenade);
            var armory = World.SpawnBuilding(1, new float3(-30f, 0f, 0f), new float2(4f, 4f), name: "Armory");
            var enemy = World.SpawnUnit(2, new float3(30f, 0f, 0f), speed: 0f);
            World.Command(1, new PlayerCommand
            {
                Type = CommandType.UseAbility, Unit = soldier, Position = new float3(30f, 0f, 0f), Argument = grenade.Id,
            });
            World.Tick();

            World.EntityManager.DestroyEntity(armory);
            World.Run(8f);

            Assert.AreEqual(100f, World.Get<Health>(enemy).Current, 1e-3f);
            Assert.IsFalse(World.IsEnabled<ActiveOrder>(soldier));
        }
    }
}
