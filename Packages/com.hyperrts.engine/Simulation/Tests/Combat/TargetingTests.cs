using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Simulation.Tests.Combat
{
    /// <summary>Targeting, stances, attack orders, minimum range, air layers and projectiles, run through the systems.</summary>
    public class TargetingTests
    {
        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2, 1);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void IdleArmedUnit_AcquiresAndKillsNearbyEnemy()
        {
            var soldier = _world.AddWeapon(_world.SpawnUnit(1, float3.zero));
            var enemy = _world.SpawnUnit(2, new float3(8f, 0f, 0f));

            _world.Run(5f);

            Assert.IsFalse(_world.EntityManager.Exists(enemy));
            Assert.IsFalse(_world.IsEnabled<AttackTarget>(soldier), "drops the target once it is gone");
            Assert.IsFalse(_world.IsEnabled<MoveDestination>(soldier), "stops where it won");
        }

        [Test]
        public void Deaths_CountAsLossesForTheOwner_AndKillsForTheAttacker()
        {
            _world.AddWeapon(_world.SpawnUnit(1, float3.zero));
            _world.SpawnUnit(2, new float3(6f, 0f, 0f));
            var outpost = _world.SpawnBuilding(2, new float3(-8f, 0f, 0f), new float2(2f, 2f));
            _world.EntityManager.SetComponentData(outpost, new Health { Current = 10f, Max = 500f });

            _world.RunUntil(() => _world.Get<PlayerStats>(_world.Player(2)) is { UnitsLost: 1, BuildingsLost: 1 }, 10f);

            var attacker = _world.Get<PlayerStats>(_world.Player(1));
            var victim = _world.Get<PlayerStats>(_world.Player(2));
            Assert.AreEqual((1, 1), (attacker.UnitsKilled, attacker.BuildingsDestroyed));
            Assert.AreEqual((1, 1), (victim.UnitsLost, victim.BuildingsLost));
            Assert.AreEqual((0, 0), (attacker.UnitsLost, victim.UnitsKilled));
        }

        [Test]
        public void PassiveStance_NeverEngages()
        {
            var soldier = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), Stance.Passive);
            var enemy = _world.SpawnUnit(2, new float3(3f, 0f, 0f));

            _world.Run(3f);

            Assert.AreEqual(100f, _world.GetHealth(enemy));
            Assert.IsFalse(_world.IsEnabled<AttackTarget>(soldier));
        }

        [Test]
        public void Allies_AreNeverTargeted()
        {
            _world.AddWeapon(_world.SpawnUnit(1, float3.zero));
            var friend = _world.SpawnUnit(1, new float3(2f, 0f, 0f));
            var ally = _world.SpawnUnit(3, new float3(-2f, 0f, 0f));

            _world.Run(3f);

            Assert.AreEqual(100f, _world.GetHealth(friend));
            Assert.AreEqual(100f, _world.GetHealth(ally));
        }

        [Test]
        public void LongRangeWeapon_HoldsFireAtUnseenEnemy_UntilAnAllySpotsIt()
        {
            var artillery = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 30f);
            var enemy = _world.SpawnUnit(2, new float3(25f, 0f, 0f));

            _world.Run(2f);

            Assert.AreEqual(100f, _world.GetHealth(enemy), "out of its team's sight");
            Assert.IsFalse(_world.IsEnabled<AttackTarget>(artillery));

            _world.SpawnUnit(3, new float3(18f, 0f, 0f), name: "Spotter");
            _world.Run(2f);

            Assert.IsTrue(WasHit(enemy), "fires once an ally sees it");
        }

        [Test]
        public void LongRangeWeapon_WithFogOff_FiresAtAnythingInRange()
        {
            _world.DisableFog();
            _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 30f);
            var enemy = _world.SpawnUnit(2, new float3(25f, 0f, 0f));

            _world.Run(2f);

            Assert.IsTrue(WasHit(enemy));
        }

        [Test]
        public void AttackOrder_ChasesBeyondAcquireRange_ThenCompletes()
        {
            var soldier = _world.AddWeapon(_world.SpawnUnit(1, float3.zero));
            var enemy = _world.SpawnUnit(2, new float3(25f, 0f, 0f), speed: 2f);
            _world.EntityManager.SetComponentData(enemy, new MoveDestination { Value = new float3(80f, 0f, 0f) });
            _world.EntityManager.SetComponentEnabled<MoveDestination>(enemy, true);
            _world.Issue(soldier, new Order { Type = OrderType.Attack, Target = enemy });

            _world.RunUntil(() => !_world.EntityManager.Exists(enemy) && _world.IsIdle(soldier), 15f);

            Assert.IsFalse(_world.EntityManager.Exists(enemy));
            Assert.IsFalse(_world.IsEnabled<ActiveOrder>(soldier), "the attack order completes");
            Assert.IsFalse(_world.IsEnabled<AttackTarget>(soldier));
            Assert.IsFalse(_world.IsEnabled<MoveDestination>(soldier));
        }

        [TestCase(Stance.Aggressive)]
        [TestCase(Stance.Defensive)]
        public void AttackMove_FightsOnTheWay_ThenReachesGoal(Stance stance)
        {
            var soldier = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), stance);
            var enemy = _world.SpawnUnit(2, new float3(50f, 0f, 0f));
            var goal = new float3(80f, 0f, 0f);
            _world.Command(1, new PlayerCommand { Type = CommandType.AttackMove, Unit = soldier, Position = goal });

            _world.RunUntil(() => !_world.EntityManager.Exists(enemy) && _world.IsIdle(soldier), 30f);

            Assert.IsFalse(_world.EntityManager.Exists(enemy));
            Assert.Less(math.distance(_world.Get<LocalTransform>(soldier).Position, goal), 1f, "resumes after the fight");
            Assert.IsFalse(_world.IsEnabled<ActiveOrder>(soldier));
        }

        [TestCase(1.5f, true)]
        [TestCase(1f, false)]
        public void DefensiveLeash_ScalesWithTheWeaponsLeashFactor(float leashFactor, bool keeps)
        {
            var soldier = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), Stance.Defensive, range: 2f);
            var weapon = _world.Get<Weapon>(soldier);
            weapon.LeashFactor = leashFactor;
            _world.EntityManager.SetComponentData(soldier, weapon);
            var enemy = _world.SpawnUnit(2, new float3(6f, 0f, 0f), speed: 0f);
            _world.Tick();

            // About 12 from its post: past a 10 acquire range, inside 1.5 times it.
            var post = new float3(-7f, 0f, 0f);
            _world.EntityManager.SetComponentData(soldier, new CombatStance { Value = Stance.Defensive, ReturnPoint = post });
            _world.EntityManager.SetComponentData(soldier, new AttackTarget { Value = enemy });
            _world.EntityManager.SetComponentEnabled<AttackTarget>(soldier, true);
            _world.Tick();

            Assert.AreEqual(keeps, _world.IsEnabled<AttackTarget>(soldier));
        }

        [Test]
        public void HoldPosition_FiresInRangeButNeverMoves()
        {
            var soldier = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), Stance.HoldPosition);
            var near = _world.SpawnUnit(2, new float3(3f, 0f, 0f));
            var far = _world.SpawnUnit(2, new float3(0f, 0f, 8f));

            _world.Run(5f);

            Assert.IsFalse(_world.EntityManager.Exists(near));
            Assert.AreEqual(100f, _world.GetHealth(far), "out of weapon range");
            Assert.AreEqual(0f, math.length(_world.Get<LocalTransform>(soldier).Position.xz), 1e-4f);
        }

        [Test]
        public void TowerUnderConstruction_HoldsFireUntilFinished()
        {
            var tower = _world.AddWeapon(
                _world.SpawnBuilding(1, float3.zero, new float2(2f, 2f), complete: false, buildTime: 100f));
            var enemy = _world.SpawnUnit(2, new float3(4f, 0f, 0f));

            _world.Run(2f);
            Assert.AreEqual(100f, _world.GetHealth(enemy));

            _world.EntityManager.SetComponentEnabled<ConstructionProgress>(tower, false);
            _world.Run(2f);
            Assert.IsFalse(_world.EntityManager.Exists(enemy), "the finished tower opens fire");
        }

        [Test]
        public void TargetInsideMinRange_IsSkipped_ForOneBeyondIt()
        {
            var gun = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 8f, minRange: 3f, cooldown: 100f);
            var near = _world.SpawnUnit(2, new float3(2f, 0f, 0f));
            var far = _world.SpawnUnit(2, new float3(-6f, 0f, 0f));

            _world.Run(1f);

            Assert.AreEqual(far, _world.Get<AttackTarget>(gun).Value);
            Assert.AreEqual(100f, _world.GetHealth(near));
            Assert.Less(_world.GetHealth(far), 100f);
        }

        [Test]
        public void OrderedTargetInsideMinRange_BacksOffToMidRange_ThenFires()
        {
            var gun = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 8f, minRange: 3f, cooldown: 100f);
            var enemy = _world.SpawnUnit(2, new float3(2f, 0f, 0f));
            _world.Command(1, new PlayerCommand
            {
                Type = CommandType.Attack, Unit = gun, Target = enemy, Position = _world.GetPosition(enemy),
            });

            _world.Run(4f);

            // Both radii are 0.5, so a 5.5 edge gap, midway between 3 and 8, is 6.5 between centres.
            var apart = math.distance(_world.GetPosition(gun).xz, _world.GetPosition(enemy).xz);
            Assert.AreEqual(6.5f, apart, 0.75f, "backed off to about mid range");
            Assert.Less(_world.GetHealth(enemy), 100f, "fired once clear of the minimum range");
        }

        [Test]
        public void SurfaceOnlyGun_IgnoresAircraft_AndCantBeOrderedAtThem()
        {
            var gun = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 6f);
            var aircraft = _world.SpawnAircraft(2, new float3(3f, 0f, 0f));
            _world.Run(2f);

            Assert.AreEqual(100f, _world.GetHealth(aircraft), "never auto-acquired");
            Assert.IsFalse(_world.IsEnabled<AttackTarget>(gun));

            _world.Command(1, new PlayerCommand
            {
                Type = CommandType.Smart, Unit = gun, Target = aircraft, Position = new float3(3f, 0f, 0f),
            });
            _world.Tick();

            Assert.AreEqual(OrderType.Move, _world.Get<ActiveOrder>(gun).Value.Type, "right-click just moves there");
        }

        [Test]
        public void AntiAirWeapon_EngagesAircraft_AndIgnoresGround()
        {
            var launcher = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 6f, damage: 10f,
                targets: WeaponTargets.Air);
            var tank = _world.SpawnUnit(2, new float3(-2f, 0f, 0f));
            var aircraft = _world.SpawnAircraft(2, new float3(3f, 0f, 0f));
            _world.Run(2f);

            Assert.Less(_world.GetHealth(aircraft), 100f);
            Assert.AreEqual(100f, _world.GetHealth(tank));
            Assert.AreEqual(aircraft, _world.Get<AttackTarget>(launcher).Value);
        }

        [Test]
        public void Projectile_LandsAfterFiring_AndArmorScalesDamage()
        {
            var bullet = _world.Own(ScriptableObject.CreateInstance<DamageType>());
            _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 8f, damage: 40f, cooldown: 100f,
                projectile: _world.ProjectilePrefab(), damageType: bullet);
            var tank = _world.SpawnUnit(2, new float3(6f, 0f, 0f));
            var writer = new EntityManagerWriter(_world.EntityManager, tank);
            writer.AddBuffer(new[] { new ArmorModifier { DamageType = bullet, Multiplier = 0.5f } });

            using var projectiles = _world.EntityManager.CreateEntityQuery(typeof(Projectile));
            Assert.IsTrue(_world.RunUntil(() => !projectiles.IsEmpty, 1f), "a projectile was launched");
            Assert.AreEqual(100f, _world.GetHealth(tank), "no damage until the projectile arrives");

            _world.Run(2f);

            Assert.AreEqual(80f, _world.GetHealth(tank), "40 damage halved by armor");
            Assert.IsTrue(projectiles.IsEmpty, "the projectile is destroyed on impact");
        }

        private bool WasHit(Entity unit) => !_world.EntityManager.Exists(unit) || _world.GetHealth(unit) < 100f;
    }
}
