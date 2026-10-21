using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Combat;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Ballistic arcs and rocket dives, flown end to end through the systems.</summary>
    public class TrajectoryTests
    {
        private const int FlightCap = 300;

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void BallisticShell_PeaksAtItsArcHeight()
        {
            SpawnShooter(Trajectory.Ballistic, range: 8f);
            _world.SpawnUnit(2, new float3(6f, 0f, 0f));

            var peak = float.MinValue;
            foreach (var point in Fly(_ => { }))
            {
                peak = math.max(peak, point.y);
            }

            // Launched and aimed 1 above the pivots, 6 apart, so the apex is 0.5 * 6 above that.
            Assert.AreEqual(4f, peak, 0.05f);
        }

        [Test]
        public void BallisticShell_LandsWhereTheTargetStood_EvenIfItMovedAndDied()
        {
            SpawnShooter(Trajectory.Ballistic, range: 8f);
            var enemy = _world.SpawnUnit(2, new float3(6f, 0f, 0f));

            var path = Fly(flown =>
            {
                if (flown == 5)
                {
                    _world.EntityManager.SetComponentData(enemy, LocalTransform.FromPosition(new float3(0f, 0f, 6f)));
                    _world.Kill(enemy);
                }
            });

            // The last frame seen is one ground step (a third of a unit) short, still high on the steep descent.
            Assert.Less(math.distance(path[^1].xz, new float2(6f, 0f)), 0.5f, "the aim stayed fixed at launch");
        }

        [Test]
        public void BallisticShell_MissesATargetThatMovedAway()
        {
            SpawnShooter(Trajectory.Ballistic, range: 8f);
            var enemy = _world.SpawnUnit(2, new float3(6f, 0f, 0f));

            Fly(flown =>
            {
                if (flown == 5)
                {
                    _world.EntityManager.SetComponentData(enemy, LocalTransform.FromPosition(new float3(6f, 0f, 3f)));
                }
            });

            Assert.AreEqual(100f, _world.GetHealth(enemy));
        }

        [Test]
        public void Rocket_ClimbsThenReachesAMovingTarget()
        {
            SpawnShooter(Trajectory.Rocket, range: 12f);
            var enemy = _world.SpawnUnit(2, new float3(6f, 0f, 0f));

            var last = float3.zero;
            var path = Fly(_ =>
            {
                last = _world.Get<LocalTransform>(enemy).Position + new float3(0.1f, 0f, 0f);
                _world.EntityManager.SetComponentData(enemy, LocalTransform.FromPosition(last));
            });

            Assert.Greater(path[1].y - path[0].y, math.abs(path[1].x - path[0].x), "it climbs steeply first");
            Assert.Less(math.distance(path[^1], last + new float3(0f, 1f, 0f)), 1f, "it dove onto the target");
            Assert.Less(_world.GetHealth(enemy), 100f);
        }

        private void SpawnShooter(Trajectory trajectory, float range) =>
            _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: range, cooldown: 100f,
                projectile: _world.ProjectilePrefab(), trajectory: trajectory, arcHeight: 0.5f);

        /// <summary>Ticks until the one shot has landed, recording its flight; the callback gets the frames flown.</summary>
        private List<float3> Fly(Action<int> beforeTick)
        {
            using var shots = _world.EntityManager.CreateEntityQuery(typeof(Projectile), typeof(LocalTransform));
            var path = new List<float3>();
            for (var frame = 0; frame < FlightCap; frame++)
            {
                beforeTick(path.Count);
                _world.Tick();
                if (shots.IsEmpty)
                {
                    if (path.Count > 0)
                    {
                        return path;
                    }

                    continue;
                }

                path.Add(shots.GetSingleton<LocalTransform>().Position);
            }

            Assert.Fail("the shot never landed");
            return path;
        }
    }
}
