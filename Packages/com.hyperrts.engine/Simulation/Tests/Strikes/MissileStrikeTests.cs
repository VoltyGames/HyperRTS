using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Cues.Sounds;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Strikes;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Tests.Strikes
{
    /// <summary>The missile strike: its launch, its warning, its blast and how defences shrink it.</summary>
    public class MissileStrikeTests : SimulationFixture
    {
        private const float Cooldown = 10f;
        private const float FlightSeconds = 3f;
        private const float Splash = 30f;
        private static readonly int Strike = EntityInfo.TypeIdFromName("Launch");
        private static readonly float3 Target = new(0f, 0f, 40f);

        private Entity _silo;

        [SetUp]
        public void Prepare()
        {
            _silo = World.SpawnBuilding(1, new float3(0f, 0f, -60f), new float2(6f, 6f));
            World.GiveAbility(_silo, new Ability
            {
                Id = Strike, Name = "Launch", Target = AbilityTarget.Point, Cooldown = Cooldown,
            }, Cooldown);
            var writer = World.Writer(_silo);
            writer.Add(new MissileStrike
            {
                AbilityId = Strike,
                Missile = World.ProjectilePrefab(),
                LaunchPoint = new float3(0f, 2f, 0f),
                Apex = 100f,
                FlightSeconds = FlightSeconds,
                Blast = new DamageProfile { Amount = 100f, SplashRadius = Splash, CoreRadius = 10f },
                CutPerDefence = 0.1f,
                MaxCut = 0.5f,
            });
            var sounds = EntitySoundsAuthoring.Add(writer);
            var cue = World.Own(ScriptableObject.CreateInstance<SoundCue>());
            EntitySoundsAuthoring.Set(sounds, CueSlot.Warning, cue);
            EntitySoundsAuthoring.Set(sounds, CueSlot.Impact, cue);
        }

        private int MissilesInFlight() => World.All<Projectile>().ToArray().Length;

        private void Fire()
        {
            World.Run(Cooldown + 0.1f);
            World.Command(1, new PlayerCommand
            {
                Type = CommandType.UseAbility, Unit = _silo, Position = Target, Argument = Strike,
            });
        }

        private void AddAirDefence(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var launcher = World.SpawnUnit(2, Target + new float3(i * 2f, 0f, 10f), new TestUnit { Speed = 0f });
                World.AddWeapon(launcher, new TestWeapon
                {
                    Stance = Stance.HoldPosition, Range = 30f, Damage = 0f, Targets = WeaponTargets.Air,
                });
            }
        }

        [Test]
        public void Firing_LaunchesFromTheSilo_AndLandsAfterItsFlight()
        {
            Fire();
            World.Tick();

            Assert.AreEqual(1, MissilesInFlight());
            var missile = World.Get<Projectile>(World.All<Projectile>().ToArray()[0]);
            Assert.AreEqual(2f, missile.Start.y, 1e-3f, "from the launch point");
            Assert.AreEqual(ProjectileKind.None, missile.Kind, "no interceptor can roll on it");

            World.Run(FlightSeconds - 0.3f);
            Assert.AreEqual(1, MissilesInFlight(), "still climbing or diving");
            Assert.IsTrue(World.RunUntil(() => MissilesInFlight() == 0, 1f));
        }

        [Test]
        public void Blast_DealsFullDamageInItsCore_AndFallsOffToItsEdge()
        {
            var core = World.SpawnUnit(2, Target + new float3(5f, 0f, 0f), new TestUnit { Radius = 0f });
            var falloff = World.SpawnUnit(2, Target + new float3(20f, 0f, 0f), new TestUnit { Radius = 0f });
            var outside = World.SpawnUnit(2, Target + new float3(35f, 0f, 0f), new TestUnit { Radius = 0f });
            Fire();

            World.Run(FlightSeconds + 1f);

            Assert.IsTrue(!World.EntityManager.Exists(core) || World.GetHealth(core) <= 0f, "full damage in the core");
            Assert.AreEqual(50f, World.GetHealth(falloff), 1e-2f, "half way from core to edge");
            Assert.AreEqual(100f, World.GetHealth(outside));
        }

        [TestCase(0, Splash)]
        [TestCase(3, Splash * 0.7f)]
        [TestCase(8, Splash * 0.5f)]
        public void Warning_ShowsTheBlastDefencesLeave(int defences, float radius)
        {
            AddAirDefence(defences);
            Fire();

            var warnings = World.QueuedOver(0.2f, CueSlot.Warning);

            Assert.AreEqual(1, warnings.Count);
            Assert.AreEqual(Target.z, warnings[0].Position.z, 1e-3f);
            Assert.AreEqual(radius, warnings[0].Radius, 1e-3f);
        }

        [Test]
        public void SwitchedOff_HoldsTheStrike()
        {
            var match = World.MatchEntity();
            var rules = World.Get<MatchRules>(match);
            rules.StrategicAssets = false;
            World.Set(match, rules);

            World.Run(Cooldown + 1f);

            Assert.AreEqual(Cooldown, World.Buffer<AbilityCooldown>(_silo)[0].Remaining, 0.01f);
        }
    }
}
