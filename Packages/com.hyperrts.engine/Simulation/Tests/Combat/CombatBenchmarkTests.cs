using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.PerformanceTesting;

namespace HyperRTS.Simulation.Tests.Combat
{
    /// <summary>
    /// Combat timings with 500 infantry a side trading fire across a 20 m gap, half by projectile, none dying. The
    /// shot and projectile jobs run single-threaded, as they append to the one damage queue.
    /// </summary>
    [Category("Benchmark")]
    public class CombatBenchmarkTests
    {
        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(new byte[] { 1, 2 }, 600f);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test, Performance]
        public void Battle_FiveHundredASide()
        {
            var projectile = _world.ProjectilePrefab();
            var units = new Entity[1000];
            for (var i = 0; i < 1000; i++)
            {
                var north = i >= 500;
                var index = i % 500;
                var sign = north ? 1f : -1f;
                var position = new float3(-50f + index % 50 * 2f, 0f, sign * (10f + index / 50 * 2f));
                units[i] = _world.SpawnUnit(north ? (byte)2 : (byte)1, position);
                _world.EntityManager.SetComponentData(units[i], new VisionRange { Value = 50f });
                _world.AddWeapon(units[i], new TestWeapon
                {
                    Range = 40f,
                    Damage = 0.1f,
                    Cooldown = 0.5f,
                    Projectile = index % 2 == 0 ? projectile : Entity.Null,
                    Turreted = true,
                });
            }

            // Until every unit has a target and the first projectiles are in flight.
            _world.Run(2f);
            var combat = _world.World.GetExistingSystemManaged<CombatSystemGroup>();
            var frame = BenchmarkKit.Median("CombatFrame", () =>
            {
                combat.Update();
                _world.EntityManager.CompleteAllTrackedJobs();
            }, () => _world.Tick());
            var shots = BenchmarkKit.Median("WeaponFire", _world.UpdateSystem<WeaponFireSystem>, () => _world.Tick());
            var flight = BenchmarkKit.Median("Projectiles", _world.UpdateSystem<ProjectileSystem>, () => _world.Tick());

            TestContext.WriteLine($"500 a side: combat {frame:F3} ms, weapon fire {shots:F3} ms, projectiles {flight:F3} ms");
            var hit = 0;
            foreach (var unit in units)
            {
                hit += _world.GetHealth(unit) < 100f ? 1 : 0;
            }

            // Everyone aims at the nearest enemy, so the two front ranks take the fire.
            Assert.GreaterOrEqual(hit, 100, $"both front ranks under fire: {hit} hit");
        }
    }
}
