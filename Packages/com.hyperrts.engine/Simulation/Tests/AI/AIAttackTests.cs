using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation.Paths;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Tests.AI
{
    /// <summary>Skirmish AI attacks: waves, fog-limited sweeps, abilities and powers, and difficulty presets.</summary>
    public class AIAttackTests : SimulationFixture
    {
        private ResourceType _supplies;

        [SetUp]
        public void Prepare()
        {
            _supplies = World.CreateSupplies();
            var writer = World.Writer(World.Player(2));
            AIPlayerSetup.Add(writer,
                new AIPlayer { ThinkInterval = 0.5f, AttackWaveSize = 2, UseAbilities = true });
        }

        [Test]
        public void AI_TrainsUnits_ThenAttacksTheEnemyBase()
        {
            TrainSoldiersAt(float3.zero);
            World.SpawnBuilding(1, new float3(60f, 0f, 0f), new float2(4f, 4f));

            World.Run(3f);

            Assert.GreaterOrEqual(AttackMovingTo(new float3(60f, 0f, 0f)), 2,
                "a wave of trained soldiers was sent at the enemy base");
            Assert.Less(World.GetResources(2, _supplies), 100, "training was paid for");
        }

        [Test]
        public void FogLimitedAI_SweepsTheMapCentre_InsteadOfAnUnseenBase()
        {
            var ai = World.Get<AIPlayer>(World.Player(2));
            ai.LimitedByFog = true;
            World.Set(World.Player(2), ai);
            TrainSoldiersAt(new float3(-40f, 0f, 0f));
            World.SpawnBuilding(1, new float3(60f, 0f, 0f), new float2(4f, 4f));

            World.Run(3f);

            Assert.AreEqual(0, AttackMovingTo(new float3(60f, 0f, 0f)), "the enemy base was never seen");
            Assert.GreaterOrEqual(AttackMovingTo(float3.zero), 2);
        }

        [Test]
        public void AIWave_MovesAsOneFormationGroup()
        {
            var squad = new Entity[3];
            for (var i = 0; i < squad.Length; i++)
            {
                squad[i] = World.SpawnUnit(2, new float3(i * 3f, 0f, 0f));
                var writer = World.Writer(squad[i]);
                var weapon = new Weapon { Range = 2f, Damage = new DamageProfile { Amount = 1f }, Cooldown = 1f };
                WeaponSetup.Add(writer, new[] { WeaponMount.Create(weapon, default) }, Stance.Aggressive, float3.zero);
            }

            World.SpawnBuilding(1, new float3(60f, 0f, 0f), new float2(4f, 4f));

            World.Run(1f);

            var group = World.Get<ActiveOrder>(squad[0]).Value.Group;
            Assert.AreNotEqual(0, group, "the wave was ordered as a formation");
            foreach (var unit in squad)
            {
                Assert.AreEqual(OrderType.AttackMove, World.Get<ActiveOrder>(unit).Value.Type);
                Assert.AreEqual(group, World.Get<ActiveOrder>(unit).Value.Group, "one command, one group");
            }

            World.EntityManager.CompleteAllTrackedJobs();
            var pathfinder = World.World.GetExistingSystem<PathfindingSystem>();
            var stats = World.World.Unmanaged.GetUnsafeSystemRef<PathfindingSystem>(pathfinder).Stats;
            Assert.LessOrEqual(stats.SearchesByCause.x, 1, "the group shares one search, not one per unit");
        }

        [Test]
        public void AI_FiresReadyAbility_AtNearestEnemyInRange()
        {
            var caster = GiveAbility(World.SpawnUnit(2, float3.zero, speed: 0f), "Snipe", AbilityTarget.Entity, 8f, 30f);
            var far = World.SpawnUnit(1, new float3(-20f, 0f, 0f), speed: 0f);
            var near = World.SpawnUnit(1, new float3(6f, 0f, 0f), speed: 0f);

            World.Run(1f);

            Assert.AreEqual(70f, World.Get<Health>(near).Current, 1e-3f, "the enemy in range was hit");
            Assert.AreEqual(100f, World.Get<Health>(far).Current, 1e-3f, "the one out of range was not");
            Assert.Greater(World.Buffer<AbilityCooldown>(caster)[0].Remaining, 0f);
        }

        [Test]
        public void AI_FiresPlayerPower_AtTheEnemyAnywhere()
        {
            World.SpawnBuilding(2, float3.zero, new float2(4f, 4f));
            var enemy = World.SpawnBuilding(1, new float3(80f, 0f, 80f), new float2(4f, 4f));
            GiveAbility(World.Player(2), "Strike", AbilityTarget.Point, 0f, 40f, radius: 3f);

            World.Run(1f);

            Assert.AreEqual(460f, World.Get<Health>(enemy).Current, 1e-3f);
        }

        [Test]
        public void TopDifficulties_ThinkFastest_AndOnlyBrutalCheats()
        {
            var match = World.Own(new GameObject("Match")).AddComponent<MatchAuthoring>();

            var hard = match.AITuningFor(AIDifficulty.Hard);
            var expert = match.AITuningFor(AIDifficulty.Expert);
            var brutal = match.AITuningFor(AIDifficulty.Brutal);
            Assert.Less(expert.thinkInterval, hard.thinkInterval);
            Assert.AreEqual(1f, expert.incomeMultiplier);
            Assert.Greater(brutal.incomeMultiplier, 1f);
            Assert.AreEqual(AIDifficulty.Brutal, brutal.ToTuning(AIDifficulty.Brutal).Difficulty);
        }

        [Test]
        public void HarderAI_ThinksSooner_AndAttacksWithSmallerWaves()
        {
            using var world = new TestWorld();
            world.CreateMatch(1, 2, 3);
            var match = world.Own(new GameObject("Match")).AddComponent<MatchAuthoring>();
            var easy = match.AITuningFor(AIDifficulty.Easy).ToComponent(AIDifficulty.Easy);
            var hard = match.AITuningFor(AIDifficulty.Hard).ToComponent(AIDifficulty.Hard);
            Assert.Greater(easy.ThinkInterval, hard.ThinkInterval);
            Assert.Greater(easy.AttackWaveSize, hard.AttackWaveSize);
            Assert.IsFalse(easy.UseAbilities);

            var easySquad = Squad(world, 2, easy, new float3(-10f, 0f, 40f));
            var hardSquad = Squad(world, 3, hard, new float3(-10f, 0f, -40f));
            world.SpawnBuilding(1, new float3(80f, 0f, 0f), new float2(4f, 4f));

            world.Run(5f);

            Assert.AreEqual(OrderType.AttackMove, world.Get<ActiveOrder>(hardSquad[0]).Value.Type, "a wave of five is enough");
            Assert.IsFalse(world.IsEnabled<ActiveOrder>(easySquad[0]), "easy waits for a bigger wave");
        }

        /// <summary>A producer of armed soldiers for slot 2 at <paramref name="at"/>, with population and money.</summary>
        private void TrainSoldiersAt(float3 at)
        {
            var soldier = World.MakePrefab(World.SpawnUnit(0, new float3(90f, 0f, 90f)));
            World.SetBuildTime(soldier, 0.5f);
            World.SetCost(soldier, _supplies, 10);
            var writer = World.Writer(soldier);
            var weapon = new Weapon
            {
                Range = 2f, Damage = new DamageProfile { Amount = 1f }, Cooldown = 1f, LeashFactor = 1.5f,
            };
            WeaponSetup.Add(writer, new[] { WeaponMount.Create(weapon, default) }, Stance.Aggressive, float3.zero);
            World.MakeProducer(World.SpawnBuilding(2, at, new float2(4f, 4f)), new float3(0f, 0f, -4f), soldier);
            World.SpawnProvider(2, at + new float3(-20f, 0f, 0f), 10);
            World.AddResources(2, _supplies, 100);
        }

        private int AttackMovingTo(float3 goal)
        {
            var count = 0;
            foreach (var unit in World.All<CombatStance>())
            {
                var order = World.Get<ActiveOrder>(unit).Value;
                var attackMoving = World.IsEnabled<ActiveOrder>(unit) && order.Type == OrderType.AttackMove;
                if (attackMoving && math.distance(order.Position, goal) < 5f)
                {
                    count++;
                }
            }

            return count;
        }

        private Entity GiveAbility(Entity entity, string name, AbilityTarget target, float range, float damage,
            float radius = 0f)
        {
            World.GiveAbility(entity, new Ability
            {
                Id = EntityInfo.TypeIdFromName(name), Name = name, Target = target,
                Filter = AbilityTargetFilter.Hostile, Range = range, Cooldown = 30f,
                Damage = new DamageProfile { Amount = damage, Radius = radius, EdgeFactor = 1f },
            });
            return entity;
        }

        private static Entity[] Squad(TestWorld world, byte faction, AIPlayer ai, float3 at)
        {
            var writer = world.Writer(world.Player(faction));
            AIPlayerSetup.Add(writer, ai);

            var squad = new Entity[5];
            for (var i = 0; i < squad.Length; i++)
            {
                squad[i] = world.AddWeapon(world.SpawnUnit(faction, at + new float3(i * 2f, 0f, 0f)));
            }

            return squad;
        }
    }
}
