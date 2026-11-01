using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Airdrops;
using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Movement;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Airdrops
{
    /// <summary>The strategic asset's map-wide charge, the bomber it sends and how air defence cuts its bombs.</summary>
    public class StrategicAssetTests : SimulationFixture
    {
        private const float Charge = 10f;
        private const int Bombs = 4;
        private static readonly int Strike = EntityInfo.TypeIdFromName("Strike");
        private static readonly float3 Target = new(0f, 0f, 40f);

        private Entity _asset;

        [SetUp]
        public void Prepare()
        {
            World.Add(World.Player(1), new PlayerSpawn { Position = new float3(0f, 0f, -90f) });
            var bomber = World.SpawnAircraft(0, float3.zero, altitude: 30f, speed: 50f);
            var stealth = World.Writer(bomber);
            StealthSetup.AddStealth(stealth, new Stealth(), enabled: true);
            World.MakePrefab(bomber);
            _asset = World.SpawnBuilding(1, new float3(0f, 0f, -60f), new float2(4f, 6f));
            World.EntityManager.AddBuffer<Ability>(_asset).Add(new Ability
            {
                Id = Strike, Name = "Strike", Target = AbilityTarget.Point, Cooldown = Charge,
                CooldownRemaining = Charge,
            });
            var writer = World.Writer(_asset);
            writer.Add(new StrategicAsset
            {
                AbilityId = Strike,
                Aircraft = bomber,
                Altitude = 30f,
                RevealSeconds = 1f,
                Bombs = new BombLoad
                {
                    Prefab = World.ProjectilePrefab(), Count = Bombs,
                    Damage = new DamageProfile { Amount = 100f, Radius = 3f, EdgeFactor = 0.5f }, Speed = 60f, ZoneRadius = 20f, MaxReduction = 0.5f,
                },
            });
        }

        private StrategicCharge ReadCharge(byte faction) => World.Get<StrategicCharge>(World.Player(faction));

        private Entity[] Bombers() => World.All<BombingFlight>().ToArray();

        private int BombsInFlight() => World.All<Projectile>().ToArray().Length;

        private void Fire()
        {
            World.Run(Charge + 0.1f);
            World.Command(1, new PlayerCommand
            {
                Type = CommandType.UseAbility, Unit = _asset, Position = Target, Argument = Strike,
            });
            World.Tick();
        }

        [Test]
        public void Charge_CountsDownForEveryone_AndPausesOnLowPower()
        {
            World.Run(2f);
            Assert.IsTrue(ReadCharge(1).HasAsset);
            Assert.AreEqual(Charge - 2f, ReadCharge(1).Remaining, 0.2f);
            Assert.IsFalse(ReadCharge(2).HasAsset);

            World.SetOffline(_asset, true);
            World.Run(3f);
            Assert.AreEqual(Charge - 2f, ReadCharge(1).Remaining, 0.2f, "paused while offline");

            World.SetOffline(_asset, false);
            World.Run(Charge);
            Assert.IsTrue(ReadCharge(1).IsReady);
        }

        [Test]
        public void LosingTheStructure_StopsTheCountdown_AndANewOwnerStartsOver()
        {
            World.Run(4f);
            World.Set(_asset, new Faction { Value = 2 });
            World.Tick();
            Assert.IsFalse(ReadCharge(1).HasAsset, "the old owner lost it");
            Assert.AreEqual(Charge, ReadCharge(2).Remaining, 0.1f, "captured, it starts over");

            World.EntityManager.DestroyEntity(_asset);
            World.Tick();
            Assert.IsFalse(ReadCharge(2).HasAsset);
        }

        [Test]
        public void SwitchedOff_HoldsTheCharge_AndShowsNoCountdown()
        {
            var match = World.MatchEntity();
            var rules = World.Get<MatchRules>(match);
            rules.StrategicAssets = false;
            World.Set(match, rules);

            World.Run(Charge + 1f);

            Assert.AreEqual(Charge, World.Buffer<Ability>(_asset)[0].CooldownRemaining, 0.01f);
            Assert.IsFalse(ReadCharge(1).HasAsset);
        }

        [Test]
        public void Firing_SendsAStealthedBomberFromTheOwnersEdge_RevealedJustBeforeTheDrop()
        {
            Fire();

            var bombers = Bombers();
            Assert.AreEqual(1, bombers.Length);
            Assert.Less(World.Get<LocalTransform>(bombers[0]).Position.z, -95f, "entered on the owner's edge");
            Assert.IsTrue(World.IsEnabled<Stealth>(bombers[0]), "undetectable on the way in");

            Assert.IsTrue(World.RunUntil(() => !World.IsEnabled<Stealth>(bombers[0]), 6f));
            var distance = math.distance(World.Get<LocalTransform>(bombers[0]).Position.xz, Target.xz);
            Assert.That(distance, Is.InRange(30f, 55f), "revealed about a second out");
            Assert.AreEqual(0, BombsInFlight(), "not dropped yet");
        }

        [Test]
        public void Bombs_SeekEnemyStructuresInTheZone_AndDamageThem()
        {
            var depot = World.SpawnBuilding(2, Target + new float3(6f, 0f, 0f), new float2(4f, 6f));
            var plant = World.SpawnBuilding(2, Target + new float3(-8f, 0f, 4f), new float2(4f, 6f));
            Fire();

            Assert.IsTrue(World.RunUntil(() => BombsInFlight() > 0, 6f));
            Assert.AreEqual(Bombs, BombsInFlight());
            foreach (var bomb in World.All<Projectile>().ToArray())
            {
                var target = World.Get<Projectile>(bomb).Hit.Target;
                Assert.IsTrue(target == depot || target == plant, "aimed at a structure");
            }

            Assert.IsTrue(World.RunUntil(() => BombsInFlight() == 0, 3f));
            Assert.Less(World.GetHealth(depot), 500f);
            Assert.Less(World.GetHealth(plant), 500f);
        }

        [Test]
        public void DropRun_OpensBeforeTheBombsLeaveTheReleasePoint()
        {
            // Slowed so the run, 90 m either side, starts after the bomber enters.
            var bomber = World.Get<StrategicAsset>(_asset).Aircraft;
            World.Set(bomber, new MovementSpeed { Value = 30f });
            World.Set(bomber, new DropRun { ReleasePoint = new float3(0f, -1.5f, 0f) });
            Fire();
            var flight = Bombers()[0];
            Assert.IsFalse(World.Get<DropRun>(flight).Active, "bay shut on the way in");

            Assert.IsTrue(World.RunUntil(() => World.Get<DropRun>(flight).Active, 6f));
            Assert.AreEqual(0, BombsInFlight(), "opens before the drop");

            Assert.IsTrue(World.RunUntil(() => BombsInFlight() > 0, 5f));
            var bomb = World.All<Projectile>().ToArray()[0];
            var plane = World.Get<LocalTransform>(flight).Position;
            Assert.AreEqual(plane.y - 1.5f, World.Get<Projectile>(bomb).Start.y, 0.1f, "from the bay");
            Assert.IsTrue(World.Get<DropRun>(flight).Active, "open as they fall");
        }

        [TestCase(1, Bombs - 1)]
        [TestCase(5, Bombs / 2)]
        public void EnemyAirDefence_RemovesABombEach_UpToHalf(int defences, int expected)
        {
            for (var i = 0; i < defences; i++)
            {
                var launcher = World.SpawnUnit(2, Target + new float3(i * 2f, 0f, 10f), speed: 0f);
                World.AddWeapon(launcher, Stance.HoldPosition, range: 30f, damage: 0f, targets: WeaponTargets.Air);
            }

            Fire();

            Assert.IsTrue(World.RunUntil(() => BombsInFlight() > 0, 6f));
            Assert.AreEqual(expected, BombsInFlight());
        }
    }
}
