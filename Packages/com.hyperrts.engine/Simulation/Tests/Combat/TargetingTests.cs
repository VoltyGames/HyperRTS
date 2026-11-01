using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Orders;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Simulation.Tests.Combat
{
    /// <summary>Targeting, stances, attack orders, minimum range, air layers and projectiles, run through the systems.</summary>
    public class TargetingTests : SimulationFixture
    {
        protected override byte[] Teams => new byte[] { 1, 2, 1 };

        [Test]
        public void IdleArmedUnit_AcquiresAndKillsNearbyEnemy()
        {
            var soldier = World.AddWeapon(World.SpawnUnit(1, float3.zero));
            var enemy = World.SpawnUnit(2, new float3(8f, 0f, 0f));

            World.Run(5f);

            Assert.IsFalse(World.EntityManager.Exists(enemy));
            Assert.IsFalse(World.IsEnabled<AttackTarget>(soldier), "drops the target once it is gone");
            Assert.IsFalse(World.IsEnabled<MoveDestination>(soldier), "stops where it won");
        }

        [Test]
        public void Deaths_CountAsLossesForTheOwner_AndKillsForTheAttacker()
        {
            World.AddWeapon(World.SpawnUnit(1, float3.zero));
            World.SpawnUnit(2, new float3(6f, 0f, 0f));
            var outpost = World.SpawnBuilding(2, new float3(-8f, 0f, 0f), new float2(2f, 2f));
            World.Set(outpost, new Health { Current = 10f, Max = 500f });

            World.RunUntil(() => World.Get<PlayerStats>(World.Player(2)) is { UnitsLost: 1, BuildingsLost: 1 }, 10f);

            var attacker = World.Get<PlayerStats>(World.Player(1));
            var victim = World.Get<PlayerStats>(World.Player(2));
            Assert.AreEqual((1, 1), (attacker.UnitsKilled, attacker.BuildingsDestroyed));
            Assert.AreEqual((1, 1), (victim.UnitsLost, victim.BuildingsLost));
            Assert.AreEqual((0, 0), (attacker.UnitsLost, victim.UnitsKilled));
        }

        [Test]
        public void PassiveStance_NeverEngages()
        {
            var soldier = World.AddWeapon(World.SpawnUnit(1, float3.zero), Stance.Passive);
            var enemy = World.SpawnUnit(2, new float3(3f, 0f, 0f));

            World.Run(3f);

            Assert.AreEqual(100f, World.GetHealth(enemy));
            Assert.IsFalse(World.IsEnabled<AttackTarget>(soldier));
        }

        [Test]
        public void Allies_AreNeverTargeted()
        {
            World.AddWeapon(World.SpawnUnit(1, float3.zero));
            var friend = World.SpawnUnit(1, new float3(2f, 0f, 0f));
            var ally = World.SpawnUnit(3, new float3(-2f, 0f, 0f));

            World.Run(3f);

            Assert.AreEqual(100f, World.GetHealth(friend));
            Assert.AreEqual(100f, World.GetHealth(ally));
        }

        [Test]
        public void LongRangeWeapon_HoldsFireAtUnseenEnemy_UntilAnAllySpotsIt()
        {
            var artillery = World.AddWeapon(World.SpawnUnit(1, float3.zero), range: 30f);
            var enemy = World.SpawnUnit(2, new float3(25f, 0f, 0f));

            World.Run(2f);

            Assert.AreEqual(100f, World.GetHealth(enemy), "out of its team's sight");
            Assert.IsFalse(World.IsEnabled<AttackTarget>(artillery));

            World.SpawnUnit(3, new float3(18f, 0f, 0f), name: "Spotter");
            World.Run(2f);

            Assert.IsTrue(WasHit(enemy), "fires once an ally sees it");
        }

        [Test]
        public void LongRangeWeapon_WithFogOff_FiresAtAnythingInRange()
        {
            World.DisableFog();
            World.AddWeapon(World.SpawnUnit(1, float3.zero), range: 30f);
            var enemy = World.SpawnUnit(2, new float3(25f, 0f, 0f));

            World.Run(2f);

            Assert.IsTrue(WasHit(enemy));
        }

        [Test]
        public void AttackOrder_ChasesBeyondAcquireRange_ThenCompletes()
        {
            var soldier = World.AddWeapon(World.SpawnUnit(1, float3.zero));
            var enemy = World.SpawnUnit(2, new float3(25f, 0f, 0f), speed: 2f);
            World.Set(enemy, new MoveDestination { Value = new float3(80f, 0f, 0f) });
            World.SetEnabled<MoveDestination>(enemy, true);
            World.Issue(soldier, new Order { Type = OrderType.Attack, Target = enemy });

            World.RunUntil(() => !World.EntityManager.Exists(enemy) && World.IsIdle(soldier), 15f);

            Assert.IsFalse(World.EntityManager.Exists(enemy));
            Assert.IsFalse(World.IsEnabled<ActiveOrder>(soldier), "the attack order completes");
            Assert.IsFalse(World.IsEnabled<AttackTarget>(soldier));
            Assert.IsFalse(World.IsEnabled<MoveDestination>(soldier));
        }

        [TestCase(Stance.Aggressive)]
        [TestCase(Stance.Defensive)]
        public void AttackMove_FightsOnTheWay_ThenReachesGoal(Stance stance)
        {
            var soldier = World.AddWeapon(World.SpawnUnit(1, float3.zero), stance);
            var enemy = World.SpawnUnit(2, new float3(50f, 0f, 0f));
            var goal = new float3(80f, 0f, 0f);
            World.Command(1, new PlayerCommand { Type = CommandType.AttackMove, Unit = soldier, Position = goal });

            World.RunUntil(() => !World.EntityManager.Exists(enemy) && World.IsIdle(soldier), 30f);

            Assert.IsFalse(World.EntityManager.Exists(enemy));
            Assert.Less(math.distance(World.Get<LocalTransform>(soldier).Position, goal), 1f, "resumes after the fight");
            Assert.IsFalse(World.IsEnabled<ActiveOrder>(soldier));
        }

        [TestCase(1.5f, true)]
        [TestCase(1f, false)]
        public void DefensiveLeash_ScalesWithTheWeaponsLeashFactor(float leashFactor, bool keeps)
        {
            var soldier = World.AddWeapon(World.SpawnUnit(1, float3.zero), Stance.Defensive, range: 2f);
            var weapon = World.Get<Weapon>(soldier);
            weapon.LeashFactor = leashFactor;
            World.Set(soldier, weapon);
            var enemy = World.SpawnUnit(2, new float3(6f, 0f, 0f), speed: 0f);
            World.Tick();

            // About 12 from its post: past a 10 acquire range, inside 1.5 times it.
            var post = new float3(-7f, 0f, 0f);
            World.Set(soldier, new CombatStance { Value = Stance.Defensive, ReturnPoint = post });
            World.Set(soldier, new AttackTarget { Value = enemy });
            World.SetEnabled<AttackTarget>(soldier, true);
            World.Tick();

            Assert.AreEqual(keeps, World.IsEnabled<AttackTarget>(soldier));
        }

        [Test]
        public void HoldPosition_FiresInRangeButNeverMoves()
        {
            var soldier = World.AddWeapon(World.SpawnUnit(1, float3.zero), Stance.HoldPosition);
            var near = World.SpawnUnit(2, new float3(3f, 0f, 0f));
            var far = World.SpawnUnit(2, new float3(0f, 0f, 8f));

            World.Run(5f);

            Assert.IsFalse(World.EntityManager.Exists(near));
            Assert.AreEqual(100f, World.GetHealth(far), "out of weapon range");
            Assert.AreEqual(0f, math.length(World.Get<LocalTransform>(soldier).Position.xz), 1e-4f);
        }

        [Test]
        public void TowerUnderConstruction_HoldsFireUntilFinished()
        {
            var tower = World.AddWeapon(
                World.SpawnBuilding(1, float3.zero, new float2(2f, 2f), complete: false, buildTime: 100f));
            var enemy = World.SpawnUnit(2, new float3(4f, 0f, 0f));

            World.Run(2f);
            Assert.AreEqual(100f, World.GetHealth(enemy));

            World.SetEnabled<ConstructionProgress>(tower, false);
            World.Run(2f);
            Assert.IsFalse(World.EntityManager.Exists(enemy), "the finished tower opens fire");
        }

        [Test]
        public void TargetInsideMinRange_IsSkipped_ForOneBeyondIt()
        {
            var gun = World.AddWeapon(World.SpawnUnit(1, float3.zero), range: 8f, minRange: 3f, cooldown: 100f);
            var near = World.SpawnUnit(2, new float3(2f, 0f, 0f));
            var far = World.SpawnUnit(2, new float3(-6f, 0f, 0f));

            World.Run(1f);

            Assert.AreEqual(far, World.Get<AttackTarget>(gun).Value);
            Assert.AreEqual(100f, World.GetHealth(near));
            Assert.Less(World.GetHealth(far), 100f);
        }

        [Test]
        public void OrderedTargetInsideMinRange_BacksOffToMidRange_ThenFires()
        {
            var gun = World.AddWeapon(World.SpawnUnit(1, float3.zero), range: 8f, minRange: 3f, cooldown: 100f);
            var enemy = World.SpawnUnit(2, new float3(2f, 0f, 0f));
            World.Command(1, new PlayerCommand
            {
                Type = CommandType.Attack, Unit = gun, Target = enemy, Position = World.GetPosition(enemy),
            });

            World.Run(4f);

            // Both radii are 0.5, so a 5.5 edge gap, midway between 3 and 8, is 6.5 between centres.
            var apart = math.distance(World.GetPosition(gun).xz, World.GetPosition(enemy).xz);
            Assert.AreEqual(6.5f, apart, 0.75f, "backed off to about mid range");
            Assert.Less(World.GetHealth(enemy), 100f, "fired once clear of the minimum range");
        }

        [Test]
        public void SurfaceOnlyGun_IgnoresAircraft_AndCantBeOrderedAtThem()
        {
            var gun = World.AddWeapon(World.SpawnUnit(1, float3.zero), range: 6f);
            var aircraft = World.SpawnAircraft(2, new float3(3f, 0f, 0f));
            World.Run(2f);

            Assert.AreEqual(100f, World.GetHealth(aircraft), "never auto-acquired");
            Assert.IsFalse(World.IsEnabled<AttackTarget>(gun));

            World.Command(1, new PlayerCommand
            {
                Type = CommandType.Smart, Unit = gun, Target = aircraft, Position = new float3(3f, 0f, 0f),
            });
            World.Tick();

            Assert.AreEqual(OrderType.Move, World.Get<ActiveOrder>(gun).Value.Type, "right-click just moves there");
        }

        [Test]
        public void AntiAirWeapon_EngagesAircraft_AndIgnoresGround()
        {
            var launcher = World.AddWeapon(World.SpawnUnit(1, float3.zero), range: 6f, damage: 10f,
                targets: WeaponTargets.Air);
            var tank = World.SpawnUnit(2, new float3(-2f, 0f, 0f));
            var aircraft = World.SpawnAircraft(2, new float3(3f, 0f, 0f));
            World.Run(2f);

            Assert.Less(World.GetHealth(aircraft), 100f);
            Assert.AreEqual(100f, World.GetHealth(tank));
            Assert.AreEqual(aircraft, World.Get<AttackTarget>(launcher).Value);
        }

        [Test]
        public void Projectile_LandsAfterFiring_AndArmorScalesDamage()
        {
            var bullet = World.Own(ScriptableObject.CreateInstance<DamageType>());
            World.AddWeapon(World.SpawnUnit(1, float3.zero), range: 8f, damage: 40f, cooldown: 100f,
                projectile: World.ProjectilePrefab(), damageType: bullet);
            var tank = World.SpawnUnit(2, new float3(6f, 0f, 0f));
            var writer = World.Writer(tank);
            writer.AddBuffer(new[] { new ArmorModifier { DamageType = bullet, Multiplier = 0.5f } });

            using var projectiles = World.Query(typeof(Projectile));
            Assert.IsTrue(World.RunUntil(() => !projectiles.IsEmpty, 1f), "a projectile was launched");
            Assert.AreEqual(100f, World.GetHealth(tank), "no damage until the projectile arrives");

            World.Run(2f);

            Assert.AreEqual(80f, World.GetHealth(tank), "40 damage halved by armor");
            Assert.IsTrue(projectiles.IsEmpty, "the projectile is destroyed on impact");
        }

        private bool WasHit(Entity unit) => !World.EntityManager.Exists(unit) || World.GetHealth(unit) < 100f;
    }
}
