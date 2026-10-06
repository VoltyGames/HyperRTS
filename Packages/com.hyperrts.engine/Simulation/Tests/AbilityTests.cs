using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Unit abilities, building superweapons and player support powers.</summary>
    public class AbilityTests
    {
        private static readonly int Grenade = EntityInfo.TypeIdFromName("Grenade");

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

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
            if (!_world.EntityManager.HasBuffer<Ability>(entity))
            {
                _world.EntityManager.AddBuffer<Ability>(entity);
            }

            _world.EntityManager.GetBuffer<Ability>(entity).Add(ability);
            return entity;
        }

        private Ability AbilityOf(Entity entity) => _world.EntityManager.GetBuffer<Ability>(entity)[0];

        private DynamicBuffer<AbilityActivation> Events()
        {
            using var query = _world.EntityManager.CreateEntityQuery(typeof(AbilityEvents));
            return _world.EntityManager.GetBuffer<AbilityActivation>(query.GetSingletonEntity());
        }

        [Test]
        public void UnitAbility_WalksIntoRange_FiresSplash_ThenCoolsDown()
        {
            var soldier = Give(_world.SpawnUnit(1, float3.zero), Spec("Grenade", AbilityTarget.Point, 5f, 20f, 50f, 3f));
            var a = _world.SpawnUnit(2, new float3(15f, 0f, 0f), speed: 0f);
            var b = _world.SpawnUnit(2, new float3(15f, 0f, 2f), speed: 0f);
            var command = new PlayerCommand
            {
                Type = CommandType.UseAbility, Unit = soldier, Position = new float3(15f, 0f, 0f), Argument = Grenade,
            };
            _world.Command(1, command);

            _world.Run(4f);

            Assert.AreEqual(50f, _world.Get<Health>(a).Current, 1e-3f);
            Assert.AreEqual(50f, _world.Get<Health>(b).Current, 1e-3f, "splash");
            Assert.Less(_world.Get<LocalTransform>(soldier).Position.x, 11f, "stopped once in range");
            Assert.Greater(AbilityOf(soldier).CooldownRemaining, 10f);
            Assert.IsFalse(_world.IsEnabled<ActiveOrder>(soldier));

            _world.Command(1, command);
            _world.Run(1f);
            Assert.AreEqual(50f, _world.Get<Health>(a).Current, 1e-3f, "still cooling down");
        }

        [Test]
        public void BuildingAbility_ChargesUp_ThenSpawnsAtTarget()
        {
            var beacon = _world.MakePrefab(_world.EntityManager.CreateEntity(typeof(LocalTransform)));
            var strike = Spec("Strike", AbilityTarget.Point, 0f, 2f);
            strike.SpawnPrefab = beacon;
            strike.CooldownRemaining = 2f;
            var silo = Give(_world.SpawnBuilding(1, float3.zero, new float2(4f, 4f)), strike);
            var command = new PlayerCommand
            {
                Type = CommandType.UseAbility, Unit = silo, Position = new float3(60f, 0f, 60f), Argument = strike.Id,
            };

            _world.Command(1, command);
            _world.Tick();
            Assert.AreEqual(0, Events().Length, "charging");

            _world.Run(2.1f);
            _world.Command(1, command);
            _world.Tick();

            Assert.AreEqual(1, Events().Length);
            Assert.AreEqual(strike.Id, Events()[0].AbilityId);
            using var beacons = _world.EntityManager.CreateEntityQuery(
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
            Give(_world.Player(1), power);
            var enemy = _world.SpawnUnit(2, new float3(40f, 0f, 40f), speed: 0f);
            var command = new PlayerCommand
            {
                Type = CommandType.UsePower, Position = new float3(40f, 0f, 40f), Argument = power.Id,
            };

            _world.Command(1, command);
            _world.Tick();
            Assert.AreEqual(100f, _world.Get<Health>(enemy).Current, "no airfield yet");

            _world.SpawnBuilding(1, new float3(-40f, 0f, 0f), new float2(4f, 4f), name: "Airfield");
            _world.Command(1, command);
            _world.Tick();

            Assert.AreEqual(70f, _world.Get<Health>(enemy).Current, 1e-3f);
            Assert.AreEqual(60f, AbilityOf(_world.Player(1)).CooldownRemaining, 0.1f);
        }

        [Test]
        public void EntityAbility_IgnoresCloakedAndGarrisonedEnemies()
        {
            var snipe = Spec("Snipe", AbilityTarget.Entity, 0f, 30f, 40f);
            Give(_world.Player(1), snipe);
            var cloaked = _world.SpawnUnit(2, new float3(20f, 0f, 0f), speed: 0f);
            var sink = new EntityManagerSink(_world.EntityManager, cloaked);
            StealthSetup.AddStealth(ref sink, new Stealth { RevealDuration = 1f }, true);
            var bunker = _world.SpawnBuilding(2, new float3(-20f, 0f, 0f), new float2(4f, 4f));
            var garrisoned = _world.SpawnUnit(2, new float3(-20f, 0f, 0f), speed: 0f);
            _world.EntityManager.AddComponentData(garrisoned, new Inside { Container = bunker });
            var visible = _world.SpawnUnit(2, new float3(0f, 0f, 20f), speed: 0f);

            foreach (var target in new[] { cloaked, garrisoned })
            {
                _world.Command(1, new PlayerCommand
                {
                    Type = CommandType.UsePower, Target = target, Argument = snipe.Id,
                });
                _world.Tick();
                Assert.AreEqual(0f, AbilityOf(_world.Player(1)).CooldownRemaining, "never fired");
            }

            _world.Command(1, new PlayerCommand { Type = CommandType.UsePower, Target = visible, Argument = snipe.Id });
            _world.Tick();
            Assert.AreEqual(60f, _world.Get<Health>(visible).Current, 1e-3f);
        }

        [Test]
        public void UnitAbility_LosingItsBuildingMidWalk_CancelsTheCast()
        {
            var grenade = Spec("Grenade", AbilityTarget.Point, 5f, 20f, 50f, 3f);
            grenade.RequiredTypeId = EntityInfo.TypeIdFromName("Armory");
            var soldier = Give(_world.SpawnUnit(1, float3.zero), grenade);
            var armory = _world.SpawnBuilding(1, new float3(-30f, 0f, 0f), new float2(4f, 4f), name: "Armory");
            var enemy = _world.SpawnUnit(2, new float3(30f, 0f, 0f), speed: 0f);
            _world.Command(1, new PlayerCommand
            {
                Type = CommandType.UseAbility, Unit = soldier, Position = new float3(30f, 0f, 0f), Argument = grenade.Id,
            });
            _world.Tick();

            _world.EntityManager.DestroyEntity(armory);
            _world.Run(8f);

            Assert.AreEqual(100f, _world.Get<Health>(enemy).Current, 1e-3f);
            Assert.IsFalse(_world.IsEnabled<ActiveOrder>(soldier));
        }
    }
}
