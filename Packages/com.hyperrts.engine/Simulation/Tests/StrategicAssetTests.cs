using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Airdrops;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Units;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>The strategic asset's map-wide charge, the bomber it sends and how air defence cuts its bombs.</summary>
    public class StrategicAssetTests
    {
        private const float Charge = 10f;
        private const int Bombs = 4;
        private static readonly int Strike = EntityInfo.TypeIdFromName("Strike");
        private static readonly float3 Target = new(0f, 0f, 40f);

        private TestWorld _world;
        private Entity _asset;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _world.EntityManager.AddComponentData(_world.Player(1), new PlayerSpawn { Position = new float3(0f, 0f, -90f) });
            var bomber = _world.SpawnAircraft(0, float3.zero, altitude: 30f, speed: 50f);
            var stealth = new EntityManagerWriter(_world.EntityManager, bomber);
            StealthSetup.AddStealth(ref stealth, new Stealth(), enabled: true);
            _world.MakePrefab(bomber);

            _asset = _world.SpawnBuilding(1, new float3(0f, 0f, -60f), new float2(4f, 6f));
            _world.EntityManager.AddBuffer<Ability>(_asset).Add(new Ability
            {
                Id = Strike, Name = "Strike", Target = AbilityTarget.Point, Cooldown = Charge,
                CooldownRemaining = Charge,
            });
            var writer = new EntityManagerWriter(_world.EntityManager, _asset);
            writer.Add(new StrategicAsset
            {
                AbilityId = Strike,
                Aircraft = bomber,
                Altitude = 30f,
                RevealSeconds = 1f,
                Bombs = new BombLoad
                {
                    Prefab = _world.ProjectilePrefab(), Count = Bombs, Damage = 100f, Radius = 3f, EdgeFactor = 0.5f,
                    Speed = 60f, ZoneRadius = 20f, MaxReduction = 0.5f,
                },
            });
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private StrategicCharge ChargeOf(byte faction) => _world.Get<StrategicCharge>(_world.Player(faction));

        private Entity[] Bombers() => _world.All<BombingFlight>().ToArray();

        private int BombsInFlight() => _world.All<Projectile>().ToArray().Length;

        private void Fire()
        {
            _world.Run(Charge + 0.1f);
            _world.Command(1, new PlayerCommand
            {
                Type = CommandType.UseAbility, Unit = _asset, Position = Target, Argument = Strike,
            });
            _world.Tick();
        }

        [Test]
        public void Charge_CountsDownForEveryone_AndPausesOnLowPower()
        {
            _world.Run(2f);
            Assert.IsTrue(ChargeOf(1).HasAsset);
            Assert.AreEqual(Charge - 2f, ChargeOf(1).Remaining, 0.2f);
            Assert.IsFalse(ChargeOf(2).HasAsset);

            _world.SetOffline(_asset, true);
            _world.Run(3f);
            Assert.AreEqual(Charge - 2f, ChargeOf(1).Remaining, 0.2f, "paused while offline");

            _world.SetOffline(_asset, false);
            _world.Run(Charge);
            Assert.IsTrue(ChargeOf(1).IsReady);
        }

        [Test]
        public void LosingTheStructure_StopsTheCountdown_AndANewOwnerStartsOver()
        {
            _world.Run(4f);
            _world.EntityManager.SetComponentData(_asset, new Faction { Value = 2 });
            _world.Tick();
            Assert.IsFalse(ChargeOf(1).HasAsset, "the old owner lost it");
            Assert.AreEqual(Charge, ChargeOf(2).Remaining, 0.1f, "captured, it starts over");

            _world.EntityManager.DestroyEntity(_asset);
            _world.Tick();
            Assert.IsFalse(ChargeOf(2).HasAsset);
        }

        [Test]
        public void SwitchedOff_HoldsTheCharge_AndShowsNoCountdown()
        {
            var match = _world.MatchEntity();
            var rules = _world.Get<MatchRules>(match);
            rules.StrategicAssets = false;
            _world.EntityManager.SetComponentData(match, rules);

            _world.Run(Charge + 1f);

            Assert.AreEqual(Charge, _world.EntityManager.GetBuffer<Ability>(_asset)[0].CooldownRemaining, 0.01f);
            Assert.IsFalse(ChargeOf(1).HasAsset);
        }

        [Test]
        public void Firing_SendsAStealthedBomberFromTheOwnersEdge_RevealedJustBeforeTheDrop()
        {
            Fire();

            var bombers = Bombers();
            Assert.AreEqual(1, bombers.Length);
            Assert.Less(_world.Get<LocalTransform>(bombers[0]).Position.z, -95f, "entered on the owner's edge");
            Assert.IsTrue(_world.IsEnabled<Stealth>(bombers[0]), "undetectable on the way in");

            Assert.IsTrue(_world.RunUntil(() => !_world.IsEnabled<Stealth>(bombers[0]), 6f));
            var distance = math.distance(_world.Get<LocalTransform>(bombers[0]).Position.xz, Target.xz);
            Assert.That(distance, Is.InRange(30f, 55f), "revealed about a second out");
            Assert.AreEqual(0, BombsInFlight(), "not dropped yet");
        }

        [Test]
        public void Bombs_SeekEnemyStructuresInTheZone_AndDamageThem()
        {
            var depot = _world.SpawnBuilding(2, Target + new float3(6f, 0f, 0f), new float2(4f, 6f));
            var plant = _world.SpawnBuilding(2, Target + new float3(-8f, 0f, 4f), new float2(4f, 6f));
            Fire();

            Assert.IsTrue(_world.RunUntil(() => BombsInFlight() > 0, 6f));
            Assert.AreEqual(Bombs, BombsInFlight());
            foreach (var bomb in _world.All<Projectile>().ToArray())
            {
                var target = _world.Get<Projectile>(bomb).Hit.Target;
                Assert.IsTrue(target == depot || target == plant, "aimed at a structure");
            }

            Assert.IsTrue(_world.RunUntil(() => BombsInFlight() == 0, 3f));
            Assert.Less(_world.GetHealth(depot), 500f);
            Assert.Less(_world.GetHealth(plant), 500f);
        }

        [Test]
        public void DropRun_OpensBeforeTheBombsLeaveTheReleasePoint()
        {
            // Slowed so the run, 90 m either side, starts after the bomber enters.
            var bomber = _world.Get<StrategicAsset>(_asset).Aircraft;
            _world.EntityManager.SetComponentData(bomber, new MovementSpeed { Value = 30f });
            _world.EntityManager.SetComponentData(bomber, new DropRun { ReleasePoint = new float3(0f, -1.5f, 0f) });
            Fire();
            var flight = Bombers()[0];
            Assert.IsFalse(_world.Get<DropRun>(flight).Active, "bay shut on the way in");

            Assert.IsTrue(_world.RunUntil(() => _world.Get<DropRun>(flight).Active, 6f));
            Assert.AreEqual(0, BombsInFlight(), "opens before the drop");

            Assert.IsTrue(_world.RunUntil(() => BombsInFlight() > 0, 5f));
            var bomb = _world.All<Projectile>().ToArray()[0];
            var plane = _world.Get<LocalTransform>(flight).Position;
            Assert.AreEqual(plane.y - 1.5f, _world.Get<Projectile>(bomb).Start.y, 0.1f, "from the bay");
            Assert.IsTrue(_world.Get<DropRun>(flight).Active, "open as they fall");
        }

        [TestCase(1, Bombs - 1)]
        [TestCase(5, Bombs / 2)]
        public void EnemyAirDefence_RemovesABombEach_UpToHalf(int defences, int expected)
        {
            for (var i = 0; i < defences; i++)
            {
                var launcher = _world.SpawnUnit(2, Target + new float3(i * 2f, 0f, 10f), speed: 0f);
                _world.AddWeapon(launcher, Stance.HoldPosition, range: 30f, damage: 0f, targets: WeaponTargets.Air);
            }

            Fire();

            Assert.IsTrue(_world.RunUntil(() => BombsInFlight() > 0, 6f));
            Assert.AreEqual(expected, BombsInFlight());
        }
    }
}
