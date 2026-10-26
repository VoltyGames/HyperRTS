using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Status;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Simulation.Tests.Combat
{
    /// <summary>The damage queue (splash, friendly fire, armor, heals) and timed status effects that change damage and stats.</summary>
    public class DamageTests
    {
        private TestWorld _world;
        private DamageType _precision;
        private DamageType _bullet;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _precision = _world.Own(ScriptableObject.CreateInstance<DamageType>());
            _bullet = _world.Own(ScriptableObject.CreateInstance<DamageType>());
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void Splash_FallsOffWithDistance_AndSparesAllies()
        {
            var centre = _world.SpawnUnit(2, float3.zero, radius: 0f);
            var edge = _world.SpawnUnit(2, new float3(4f, 0f, 0f), radius: 0f);
            var outside = _world.SpawnUnit(2, new float3(6f, 0f, 0f), radius: 0f);
            var friend = _world.SpawnUnit(1, new float3(1f, 0f, 0f), radius: 0f);
            _world.Tick();

            _world.QueueDamage(Blast(float3.zero, 4f));
            _world.Tick();

            Assert.AreEqual(60f, _world.GetHealth(centre), 1e-3f, "full damage at the centre");
            Assert.AreEqual(80f, _world.GetHealth(edge), 1e-3f, "edge factor at the radius");
            Assert.AreEqual(100f, _world.GetHealth(outside));
            Assert.AreEqual(100f, _world.GetHealth(friend), "splash spares allies by default");
        }

        [Test]
        public void FriendlyFire_HurtsAllies()
        {
            var friend = _world.SpawnUnit(1, float3.zero, radius: 0f);
            _world.Tick();

            _world.QueueDamage(Blast(float3.zero, 2f, friendlyFire: true));
            _world.Tick();

            Assert.AreEqual(60f, _world.GetHealth(friend), 1e-3f);
        }

        [TestCase(0f, 0f, 10f, 0.5f)]
        [TestCase(10f, 0f, 0f, 1f)]
        [TestCase(0f, 0f, -10f, 2f)]
        public void DirectionalArmor_ScalesByHitSide(float x, float y, float z, float multiplier)
        {
            var tank = _world.SpawnUnit(2, float3.zero);
            var writer = new EntityManagerWriter(_world.EntityManager, tank);
            writer.Add(new ArmorFacing { Front = 0.5f, Side = 1f, Rear = 2f });

            _world.QueueDamage(new DamageEvent { Target = tank, Origin = new float3(x, y, z), SourceFaction = 1, Amount = 20f });
            _world.Tick();

            Assert.AreEqual(100f - 20f * multiplier, _world.GetHealth(tank), 1e-3f);
        }

        [Test]
        public void DamageTakenStat_AndKillCredit_AreApplied()
        {
            var shooter = _world.SpawnUnit(1, new float3(-5f, 0f, 0f));
            var target = _world.SpawnUnit(2, float3.zero);
            var source = new StatSource(StatSourceKind.Upgrade, 99);
            _world.EntityManager.GetBuffer<StatModifier>(target)
                .Add(new StatModifier { Stat = Stat.DamageTaken, Percent = -0.25f, Source = source });

            _world.QueueDamage(new DamageEvent { Target = target, Source = shooter, SourceFaction = 1, Amount = 40f });
            _world.Tick();

            Assert.AreEqual(70f, _world.GetHealth(target), 1e-3f);
            Assert.AreEqual(shooter, _world.Get<LastAttacker>(target).Source);
        }

        [Test]
        public void NegativeDamage_HealsUpToMax()
        {
            var unit = _world.SpawnUnit(1, float3.zero);
            _world.EntityManager.SetComponentData(unit, new Health { Current = 50f, Max = 100f });

            _world.QueueDamage(new DamageEvent { Target = unit, Amount = -80f });
            _world.Tick();

            Assert.AreEqual(100f, _world.GetHealth(unit));
        }

        [Test]
        public void SplashWeapon_HitsTargetAndNeighbours()
        {
            var gun = _world.SpawnUnit(1, float3.zero);
            var writer = new EntityManagerWriter(_world.EntityManager, gun);
            WeaponSetup.Add(ref writer, new Weapon
            {
                Range = 8f, Damage = 30f, Cooldown = 100f, SplashRadius = 3f, SplashEdgeFactor = 1f,
                LeashFactor = 1.5f,
            }, Stance.Aggressive, float3.zero, 0);
            var target = _world.SpawnUnit(2, new float3(6f, 0f, 0f));
            var neighbour = _world.SpawnUnit(2, new float3(6f, 0f, 1.5f));
            var aircraft = _world.SpawnAircraft(2, new float3(6f, 0f, -1f));

            _world.Run(1f);

            Assert.AreEqual(70f, _world.GetHealth(target), 1e-3f);
            Assert.AreEqual(70f, _world.GetHealth(neighbour), 1e-3f);
            Assert.AreEqual(100f, _world.GetHealth(aircraft), "splash stays on the surface");
            Assert.AreEqual(0f, math.length(_world.Get<LocalTransform>(gun).Position.xz), 1e-3f);
        }

        [Test]
        public void TypedBonus_RaisesOnlyMatchingHits_UntilExpiry()
        {
            var target = _world.SpawnUnit(2, float3.zero, speed: 0f);
            Apply(target, Marked(duration: 2f));

            Hit(target, _precision, 10f);
            Assert.AreEqual(87f, _world.GetHealth(target), 1e-3f);
            Hit(target, _bullet, 10f);
            Assert.AreEqual(77f, _world.GetHealth(target), 1e-3f, "other damage types are unchanged");

            _world.Run(2.1f);
            Assert.AreEqual(0, Statuses(target).Length);
            Assert.AreEqual(0, _world.EntityManager.GetBuffer<DamageTakenModifier>(target).Length);
            Hit(target, _precision, 10f);
            Assert.AreEqual(67f, _world.GetHealth(target), 1e-3f, "expired");
        }

        [Test]
        public void RefreshRule_ResetsDuration_WithoutStacking()
        {
            var target = _world.SpawnUnit(2, float3.zero, speed: 0f);
            var marked = Marked(duration: 2f);
            Apply(target, marked);
            _world.Run(1.5f);
            Apply(target, marked);

            _world.Run(1f);
            Assert.AreEqual(1, Statuses(target).Length, "outlasts the first application");
            Assert.AreEqual(1, Statuses(target)[0].Stacks);
            Assert.AreEqual(1, _world.EntityManager.GetBuffer<DamageTakenModifier>(target).Length);

            _world.Run(1.2f);
            Assert.AreEqual(0, Statuses(target).Length);
        }

        [Test]
        public void AddStackRule_StacksToMax()
        {
            var target = _world.SpawnUnit(2, float3.zero, speed: 0f);
            var marked = Marked(duration: 10f, StatusStackRule.AddStack, maxStacks: 2);
            for (var i = 0; i < 3; i++)
            {
                Apply(target, marked);
            }

            Assert.AreEqual(2, Statuses(target)[0].Stacks);
            var modifiers = _world.EntityManager.GetBuffer<DamageTakenModifier>(target);
            Assert.AreEqual(1, modifiers.Length);
            Assert.AreEqual(0.6f, modifiers[0].Percent, 1e-5f);
        }

        [Test]
        public void StatBonus_AppliesWhileActive_AndRestoresAfter()
        {
            var unit = _world.SpawnUnit(1, float3.zero, speed: 0f);
            _world.EntityManager.SetComponentData(unit, new MovementSpeed { Value = 4f });
            _world.Tick();
            var haste = Effect("Haste", new StatusEffectDefinition { Duration = 1f, MaxStacks = 1 },
                new[] { new StatBonus { stat = Stat.MoveSpeed, percent = 0.5f } });

            Apply(unit, haste);
            Assert.AreEqual(6f, _world.Get<MovementSpeed>(unit).Value, 1e-3f);

            _world.Run(1.1f);
            Assert.AreEqual(4f, _world.Get<MovementSpeed>(unit).Value, 1e-3f);
            Assert.AreEqual(0, _world.EntityManager.GetBuffer<StatModifier>(unit).Length);
        }

        [Test]
        public void DamageOverTime_HurtsInTheFrameItApplies_AndCreditsTheSource()
        {
            var attacker = _world.SpawnUnit(1, new float3(-5f, 0f, 0f), speed: 0f);
            var target = _world.SpawnUnit(2, float3.zero, speed: 0f);
            var burn = Effect("Burn", new StatusEffectDefinition
            {
                Duration = 1f, MaxStacks = 1, DamagePerSecond = 10f, DamageType = _bullet,
            });

            Apply(target, burn, attacker, 1);
            Assert.Less(_world.GetHealth(target), 100f, "the first tick hurts in the frame the effect applies");
            _world.Run(1.5f);

            Assert.AreEqual(90f, _world.GetHealth(target), 1f);
            Assert.AreEqual(attacker, _world.Get<LastAttacker>(target).Source);
        }

        [Test]
        public void EntityAbility_AppliesItsStatusEffect_ToTheTarget()
        {
            var marked = Marked(duration: 10f);
            var ability = new Ability
            {
                Id = EntityInfo.TypeIdFromName("Mark"),
                Name = "Mark",
                Target = AbilityTarget.Entity,
                Filter = AbilityTargetFilter.Hostile,
                Cooldown = 30f,
                StatusEffect = marked,
            };
            _world.EntityManager.AddBuffer<Ability>(_world.Player(1)).Add(ability);
            var enemy = _world.SpawnUnit(2, new float3(0f, 0f, 20f), speed: 0f);

            _world.Command(1, new PlayerCommand { Type = CommandType.UsePower, Target = enemy, Argument = ability.Id });
            _world.Tick();

            Assert.AreEqual(1, Statuses(enemy).Length);
            Assert.AreEqual(EntityInfo.TypeIdFromName("Marked"), Statuses(enemy)[0].TypeId);
        }

        private static DamageEvent Blast(float3 position, float radius, bool friendlyFire = false) => new()
        {
            Position = position,
            Origin = position,
            SourceFaction = 1,
            Amount = 40f,
            Radius = radius,
            EdgeFactor = 0.5f,
            FriendlyFire = friendlyFire,
        };

        private Entity Effect(string name, StatusEffectDefinition definition, StatBonus[] bonuses = null,
            TypedDamageBonus[] damageBonuses = null)
        {
            var entity = _world.EntityManager.CreateEntity();
            var writer = new EntityManagerWriter(_world.EntityManager, entity);
            var info = new EntityInfo { TypeId = EntityInfo.TypeIdFromName(name), Name = name };
            StatusEffectAuthoring.Add(ref writer, info, definition, bonuses ?? new StatBonus[0],
                damageBonuses ?? new TypedDamageBonus[0]);
            return _world.MakePrefab(entity);
        }

        private Entity Marked(float duration, StatusStackRule rule = StatusStackRule.Refresh, int maxStacks = 1) =>
            Effect("Marked", new StatusEffectDefinition { Duration = duration, Rule = rule, MaxStacks = maxStacks },
                damageBonuses: new[] { new TypedDamageBonus { damageType = _precision, percent = 0.3f } });

        private void Apply(Entity target, Entity effect, Entity source = default, byte sourceFaction = 0)
        {
            using var query = _world.EntityManager.CreateEntityQuery(typeof(StatusQueue));
            _world.EntityManager.GetBuffer<StatusRequest>(query.GetSingletonEntity()).Add(new StatusRequest
            {
                Target = target, Definition = effect, Source = source, SourceFaction = sourceFaction,
            });
            _world.Tick();
        }

        private void Hit(Entity target, DamageType type, float amount)
        {
            _world.QueueDamage(new DamageEvent { Target = target, Amount = amount, Type = type });
            _world.Tick();
        }

        private DynamicBuffer<ActiveStatus> Statuses(Entity entity) =>
            _world.EntityManager.GetBuffer<ActiveStatus>(entity);
    }
}
