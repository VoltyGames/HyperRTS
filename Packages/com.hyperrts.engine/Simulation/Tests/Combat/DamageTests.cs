using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Death;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Stats;
using HyperRTS.Simulation.Status;
using HyperRTS.Simulation.Movement;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Simulation.Tests.Combat
{
    /// <summary>The damage queue (splash, friendly fire, armor, heals) and timed status effects that change damage and stats.</summary>
    public class DamageTests : SimulationFixture
    {
        private DamageType _precision;
        private DamageType _bullet;

        [SetUp]
        public void Prepare()
        {
            _precision = World.Own(ScriptableObject.CreateInstance<DamageType>());
            _bullet = World.Own(ScriptableObject.CreateInstance<DamageType>());
        }

        [Test]
        public void Splash_FallsOffWithDistance_AndSparesAllies()
        {
            var centre = World.SpawnUnit(2, float3.zero, radius: 0f);
            var edge = World.SpawnUnit(2, new float3(4f, 0f, 0f), radius: 0f);
            var outside = World.SpawnUnit(2, new float3(6f, 0f, 0f), radius: 0f);
            var friend = World.SpawnUnit(1, new float3(1f, 0f, 0f), radius: 0f);
            World.Tick();

            World.QueueDamage(Blast(float3.zero, 4f));
            World.Tick();

            Assert.AreEqual(60f, World.GetHealth(centre), 1e-3f, "full damage at the centre");
            Assert.AreEqual(80f, World.GetHealth(edge), 1e-3f, "edge factor at the radius");
            Assert.AreEqual(100f, World.GetHealth(outside));
            Assert.AreEqual(100f, World.GetHealth(friend), "splash spares allies by default");
        }

        [Test]
        public void FriendlyFire_HurtsAllies()
        {
            var friend = World.SpawnUnit(1, float3.zero, radius: 0f);
            World.Tick();

            World.QueueDamage(Blast(float3.zero, 2f, friendlyFire: true));
            World.Tick();

            Assert.AreEqual(60f, World.GetHealth(friend), 1e-3f);
        }

        [TestCase(0f, 0f, 10f, 0.5f)]
        [TestCase(10f, 0f, 0f, 1f)]
        [TestCase(0f, 0f, -10f, 2f)]
        public void DirectionalArmor_ScalesByHitSide(float x, float y, float z, float multiplier)
        {
            var tank = World.SpawnUnit(2, float3.zero);
            var writer = World.Writer(tank);
            writer.Add(new ArmorFacing { Front = 0.5f, Side = 1f, Rear = 2f });

            World.QueueDamage(new DamageEvent { Target = tank, Origin = new float3(x, y, z), SourceFaction = 1, Profile = new DamageProfile { Amount = 20f } });
            World.Tick();

            Assert.AreEqual(100f - 20f * multiplier, World.GetHealth(tank), 1e-3f);
        }

        [Test]
        public void DamageTakenStat_AndKillCredit_AreApplied()
        {
            var shooter = World.SpawnUnit(1, new float3(-5f, 0f, 0f));
            var target = World.SpawnUnit(2, float3.zero);
            var source = new StatSource(StatSourceKind.Upgrade, 99);
            World.Buffer<StatModifier>(target)
                .Add(new StatModifier { Stat = Stat.DamageTaken, Percent = -0.25f, Source = source });

            World.QueueDamage(new DamageEvent { Target = target, Source = shooter, SourceFaction = 1, Profile = new DamageProfile { Amount = 40f } });
            World.Tick();

            Assert.AreEqual(70f, World.GetHealth(target), 1e-3f);
            Assert.AreEqual(shooter, World.Get<LastAttacker>(target).Source);
        }

        [Test]
        public void NegativeDamage_HealsUpToMax()
        {
            var unit = World.SpawnUnit(1, float3.zero);
            World.Set(unit, new Health { Current = 50f, Max = 100f });

            World.QueueDamage(new DamageEvent { Target = unit, Profile = new DamageProfile { Amount = -80f } });
            World.Tick();

            Assert.AreEqual(100f, World.GetHealth(unit));
        }

        [Test]
        public void SplashWeapon_HitsTargetAndNeighbours()
        {
            var gun = World.SpawnUnit(1, float3.zero);
            var writer = World.Writer(gun);
            var weapon = new Weapon
            {
                Range = 8f, Damage = new DamageProfile { Amount = 30f, Radius = 3f, EdgeFactor = 1f }, Cooldown = 100f,
            };
            WeaponSetup.Add(writer, new[] { WeaponMount.Create(weapon, default) }, CombatStance.Create(Stance.Aggressive, float3.zero));
            var target = World.SpawnUnit(2, new float3(6f, 0f, 0f));
            var neighbour = World.SpawnUnit(2, new float3(6f, 0f, 1.5f));
            var aircraft = World.SpawnAircraft(2, new float3(6f, 0f, -1f));

            World.Run(1f);

            Assert.AreEqual(70f, World.GetHealth(target), 1e-3f);
            Assert.AreEqual(70f, World.GetHealth(neighbour), 1e-3f);
            Assert.AreEqual(100f, World.GetHealth(aircraft), "splash stays on the surface");
            Assert.AreEqual(0f, math.length(World.Get<LocalTransform>(gun).Position.xz), 1e-3f);
        }

        [Test]
        public void TypedBonus_RaisesOnlyMatchingHits_UntilExpiry()
        {
            var target = World.SpawnUnit(2, float3.zero, speed: 0f);
            Apply(target, Marked(duration: 2f));

            Hit(target, _precision, 10f);
            Assert.AreEqual(87f, World.GetHealth(target), 1e-3f);
            Hit(target, _bullet, 10f);
            Assert.AreEqual(77f, World.GetHealth(target), 1e-3f, "other damage types are unchanged");

            World.Run(2.1f);
            Assert.AreEqual(0, Statuses(target).Length);
            Assert.AreEqual(0, World.Buffer<StatModifier>(target).Length);
            Hit(target, _precision, 10f);
            Assert.AreEqual(67f, World.GetHealth(target), 1e-3f, "expired");
        }

        [Test]
        public void RefreshRule_ResetsDuration_WithoutStacking()
        {
            var target = World.SpawnUnit(2, float3.zero, speed: 0f);
            var marked = Marked(duration: 2f);
            Apply(target, marked);
            World.Run(1.5f);
            Apply(target, marked);

            World.Run(1f);
            Assert.AreEqual(1, Statuses(target).Length, "outlasts the first application");
            Assert.AreEqual(1, Statuses(target)[0].Stacks);
            Assert.AreEqual(1, World.Buffer<StatModifier>(target).Length);

            World.Run(1.2f);
            Assert.AreEqual(0, Statuses(target).Length);
        }

        [Test]
        public void AddStackRule_StacksToMax()
        {
            var target = World.SpawnUnit(2, float3.zero, speed: 0f);
            var marked = Marked(duration: 10f, StatusStackRule.AddStack, maxStacks: 2);
            for (var i = 0; i < 3; i++)
            {
                Apply(target, marked);
            }

            Assert.AreEqual(2, Statuses(target)[0].Stacks);
            var modifiers = World.Buffer<StatModifier>(target);
            Assert.AreEqual(1, modifiers.Length);
            Assert.AreEqual(0.6f, modifiers[0].Percent, 1e-5f);
        }

        [Test]
        public void StatBonus_AppliesWhileActive_AndRestoresAfter()
        {
            var unit = World.SpawnUnit(1, float3.zero, speed: 0f);
            World.Set(unit, new MovementSpeed { Value = 4f });
            World.Tick();
            var haste = Effect("Haste", new StatusEffectDefinition { Duration = 1f, MaxStacks = 1 },
                new[] { new StatBonus { stat = Stat.MoveSpeed, percent = 0.5f } });

            Apply(unit, haste);
            Assert.AreEqual(6f, World.Get<MovementSpeed>(unit).Value, 1e-3f);

            World.Run(1.1f);
            Assert.AreEqual(4f, World.Get<MovementSpeed>(unit).Value, 1e-3f);
            Assert.AreEqual(0, World.Buffer<StatModifier>(unit).Length);
        }

        [Test]
        public void DamageOverTime_HurtsInTheFrameItApplies_AndCreditsTheSource()
        {
            var attacker = World.SpawnUnit(1, new float3(-5f, 0f, 0f), speed: 0f);
            var target = World.SpawnUnit(2, float3.zero, speed: 0f);
            var burn = Effect("Burn", new StatusEffectDefinition
            {
                Duration = 1f, MaxStacks = 1, DamagePerSecond = 10f, Damage = DamageProfile.FromType(_bullet),
            });

            Apply(target, burn, attacker, 1);
            Assert.Less(World.GetHealth(target), 100f, "the first tick hurts in the frame the effect applies");
            World.Run(1.5f);

            Assert.AreEqual(90f, World.GetHealth(target), 1f);
            Assert.AreEqual(attacker, World.Get<LastAttacker>(target).Source);
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
            World.GiveAbility(World.Player(1), ability);
            var enemy = World.SpawnUnit(2, new float3(0f, 0f, 20f), speed: 0f);

            World.Command(1, new PlayerCommand { Type = CommandType.UsePower, Target = enemy, Argument = ability.Id });
            World.Tick();

            Assert.AreEqual(1, Statuses(enemy).Length);
            Assert.AreEqual(EntityInfo.TypeIdFromName("Marked"), Statuses(enemy)[0].TypeId);
        }

        private static DamageEvent Blast(float3 position, float radius, bool friendlyFire = false) => new()
        {
            Position = position,
            Origin = position,
            SourceFaction = 1,
            Profile = new DamageProfile { Amount = 40f, Radius = radius, EdgeFactor = 0.5f, FriendlyFire = friendlyFire },
        };

        private Entity Effect(string name, StatusEffectDefinition definition, StatBonus[] bonuses = null)
        {
            var entity = World.EntityManager.CreateEntity();
            var writer = World.Writer(entity);
            var info = new EntityInfo { TypeId = EntityInfo.TypeIdFromName(name), Name = name };
            StatusEffectAuthoring.Add(writer, info, definition, bonuses ?? new StatBonus[0]);
            return World.MakePrefab(entity);
        }

        private Entity Marked(float duration, StatusStackRule rule = StatusStackRule.Refresh, int maxStacks = 1) =>
            Effect("Marked", new StatusEffectDefinition { Duration = duration, Rule = rule, MaxStacks = maxStacks },
                new[] { new StatBonus { stat = Stat.DamageTaken, percent = 0.3f, damageType = _precision } });

        private void Apply(Entity target, Entity effect, Entity source = default, byte sourceFaction = 0)
        {
            using var query = World.Query(typeof(StatusQueue));
            World.EntityManager.GetBuffer<StatusRequest>(query.GetSingletonEntity()).Add(new StatusRequest
            {
                Target = target, Definition = effect, Source = source, SourceFaction = sourceFaction,
            });
            World.Tick();
        }

        private void Hit(Entity target, DamageType type, float amount)
        {
            World.QueueDamage(new DamageEvent { Target = target, Profile = new DamageProfile { Amount = amount, Type = type } });
            World.Tick();
        }

        private DynamicBuffer<ActiveStatus> Statuses(Entity entity) =>
            World.Buffer<ActiveStatus>(entity);
    }
}
