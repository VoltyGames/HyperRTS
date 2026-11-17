using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Movement;
using HyperRTS.Simulation.Strikes;
using HyperRTS.Simulation.Vision.Detection;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Strikes
{
    /// <summary>The strategic asset's map-wide charge, the bomber strike it fires and how air defence cuts its bombs.</summary>
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
            World.GiveAbility(_asset, new Ability
            {
                Id = Strike, Name = "Strike", Target = AbilityTarget.Point, Cooldown = Charge,
            }, Charge);
            var writer = World.Writer(_asset);
            BomberStrikeAuthoring.Add(writer, Strike, new BomberStrike
            {
                Aircraft = bomber,
                Altitude = 30f,
                RevealSeconds = 1f,
                Bombs = new BombLoad
                {
                    Prefab = World.ProjectilePrefab(), Count = Bombs,
                    Damage = new DamageProfile { Amount = 100f, SplashRadius = 3f, EdgeFactor = 0.5f }, Speed = 60f, ZoneRadius = 20f, MaxReduction = 0.5f,
                },
            }, strategic: true);
            writer.AddBuffer(new[] { new StrikeAbility { AbilityId = Strike, Reach = 23f, Kind = StrikeKind.Bomber } });
        }

        private StrategicCharge ReadCharge(byte owner) => World.Get<StrategicCharge>(World.Player(owner));

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
            World.Set(_asset, new Owner { Value = 2 });
            World.Tick();
            Assert.IsFalse(ReadCharge(1).HasAsset, "the old owner lost it");
            Assert.AreEqual(Charge, ReadCharge(2).Remaining, 0.1f, "captured, it starts over");

            World.EntityManager.DestroyEntity(_asset);
            World.Tick();
            Assert.IsFalse(ReadCharge(2).HasAsset);
        }

        [Test]
        public void SwitchedOff_RefusesTheStrike_AndShowsNoCountdown()
        {
            SwitchOff();

            Fire();
            World.Run(1f);

            Assert.AreEqual(0, Bombers().Length);
            Assert.IsFalse(ReadCharge(1).HasAsset);
            Assert.IsTrue(World.Buffer<AbilityCooldown>(_asset)[0].IsReady, "refused, not spent");
        }

        [Test]
        public void SwitchedOff_StrategicWeaponsDontFire()
        {
            var launcher = World.AddWeapon(World.SpawnUnit(1, new float3(20f, 0f, 0f)), new TestWeapon { Range = 6f });
            World.SetAmmo(launcher, 1, 1f, AmmoRefill.Magazine);
            var weapon = World.Primary(launcher);
            weapon.Strategic = true;
            World.SetPrimary(launcher, weapon);
            SwitchOff();
            var enemy = World.SpawnUnit(2, new float3(20f, 0f, 4f));

            World.Run(3f);

            Assert.AreEqual(1, World.PrimaryAmmo(launcher).Current);
            Assert.AreEqual(100f, World.GetHealth(enemy));
        }

        [Test]
        public void StructureWithBothStrikes_LaunchesTheOneFired()
        {
            var launch = EntityInfo.TypeIdFromName("Launch");
            World.GiveAbility(_asset, new Ability { Id = launch, Name = "Launch", Target = AbilityTarget.Point });
            MissileStrikeAuthoring.Add(World.Writer(_asset), launch, new MissileStrike
            {
                Missile = World.ProjectilePrefab(), Apex = 50f, FlightSeconds = 2f,
                Blast = new DamageProfile { Amount = 1f, SplashRadius = 10f },
            }, strategic: false);
            World.Buffer<StrikeAbility>(_asset).Add(new StrikeAbility
            {
                AbilityId = launch, Reach = 10f, Kind = StrikeKind.Missile,
            });

            World.Command(1, new PlayerCommand
            {
                Type = CommandType.UseAbility, Unit = _asset, Position = Target, Argument = launch,
            });
            World.Tick();

            Assert.AreEqual(1, BombsInFlight(), "the missile");
            Assert.AreEqual(0, Bombers().Length);
        }

        private void SwitchOff()
        {
            var match = World.MatchEntity();
            var rules = World.Get<MatchRules>(match);
            rules.StrategicAssets = false;
            World.Set(match, rules);
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
        public void Bombs_LeaveFromTheBombersBay()
        {
            var bomber = World.Get<BomberStrike>(_asset).Aircraft;
            World.Set(bomber, new DropRun { ReleasePoint = new float3(0f, -1.5f, 0f) });
            Fire();
            var flight = Bombers()[0];

            Assert.IsTrue(World.RunUntil(() => BombsInFlight() > 0, 8f));
            var bomb = World.All<Projectile>().ToArray()[0];
            var plane = World.Get<LocalTransform>(flight).Position;
            Assert.AreEqual(plane.y - 1.5f, World.Get<Projectile>(bomb).Start.y, 0.1f, "from the bay");
        }

        [TestCase(1, Bombs - 1)]
        [TestCase(5, Bombs / 2)]
        public void EnemyAirDefence_RemovesABombEach_UpToHalf(int defences, int expected)
        {
            for (var i = 0; i < defences; i++)
            {
                var launcher = World.SpawnUnit(2, Target + new float3(i * 2f, 0f, 10f), new TestUnit { Speed = 0f });
                World.AddWeapon(launcher, new TestWeapon
                {
                    Stance = Stance.HoldPosition,
                    Range = 30f,
                    Damage = 0f,
                    Targets = WeaponTargets.Air,
                });
            }

            Fire();

            Assert.IsTrue(World.RunUntil(() => BombsInFlight() > 0, 6f));
            Assert.AreEqual(expected, BombsInFlight());
        }
    }
}
