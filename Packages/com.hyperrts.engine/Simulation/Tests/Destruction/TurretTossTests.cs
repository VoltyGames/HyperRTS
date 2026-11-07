using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Death;
using HyperRTS.Simulation.Navigation.Areas;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Simulation.Tests.Destruction
{
    /// <summary>A catastrophic death throws the turret into its arc, where it blocks its cell; other deaths leave a whole wreck.</summary>
    public class TurretTossTests
    {
        private const float MinDistance = 4f;
        private const float MaxDistance = 9f;
        private const float Arc = math.PI * 0.5f;
        private const float HeadingStep = 2f * math.PI / 256f;
        private const byte HullMark = 7;
        private const byte WreckMark = 8;

        private TestWorld _world;
        private DamageType _tankRound;
        private DamageType _bullet;
        private Entity _hull;
        private Entity _turret;
        private Entity _wreck;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _tankRound = ScriptableObject.CreateInstance<DamageType>();
            _bullet = ScriptableObject.CreateInstance<DamageType>();
            _hull = MakeRemains(HullMark);
            _wreck = MakeRemains(WreckMark);
            _turret = MakeRemains(0);
            _world.EntityManager.AddComponent<TossedTurret>(_turret);
            var writer = _world.Writer(_turret);
            NavSetup.AddObstacle(writer, new float2(2f, 2f));
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
            Object.DestroyImmediate(_tankRound);
            Object.DestroyImmediate(_bullet);
        }

        [Test]
        public void TankRoundKill_ThrowsTheTurretIntoItsArc_AndItBlocksItsCell()
        {
            for (var shot = 0; shot < 6; shot++)
            {
                var start = new float3(shot * 40f, 0f, 0f);

                Shoot(SpawnTank(start), _tankRound, 120f, start);
                _world.Tick();

                var landing = NearestTurret(start);
                var offset = landing - start;
                Assert.That(math.length(offset), Is.InRange(MinDistance - 1e-3f, MaxDistance + 1e-3f));
                var off = math.abs(math.atan2(offset.x, offset.z));
                Assert.LessOrEqual(off, Arc * 0.5f + HeadingStep, "inside the arc ahead of the shot");
                Assert.IsFalse(_world.Grid().IsWalkable(landing), "the turret holds its cell");
                Assert.AreEqual(shot + 1, Remains(HullMark), "the hull is the wreck that is left");
            }
        }

        [Test]
        public void TheSameKill_LandsTheTurretInTheSamePlace()
        {
            Shoot(SpawnTank(float3.zero), _tankRound, 120f, float3.zero);
            var landing = NearestTurret(float3.zero);
            _world.Dispose();
            SetUp();

            Shoot(SpawnTank(float3.zero), _tankRound, 120f, float3.zero);

            MathAssert.Near(landing, NearestTurret(float3.zero), 1e-4f);
        }

        [TestCase(120f, false, Description = "a bullet that kills with little to spare")]
        [TestCase(300f, true, Description = "a bullet that overshoots by more than the share")]
        public void BulletKill_ThrowsTheTurretOnlyWhenItOverkills(float damage, bool thrown)
        {
            Shoot(SpawnTank(float3.zero), _bullet, damage, float3.zero);

            using var turrets = _world.All<TossedTurret>();
            Assert.AreEqual(thrown ? 1 : 0, turrets.Length);
            Assert.AreEqual(thrown ? 1 : 0, Remains(HullMark));
            Assert.AreEqual(thrown ? 0 : 1, Remains(WreckMark));
        }

        private Entity MakeRemains(byte mark)
        {
            var remains = _world.EntityManager.CreateEntity(typeof(LocalTransform));
            _world.Add(remains, new Faction { Value = mark });
            return _world.MakePrefab(remains);
        }

        private Entity SpawnTank(float3 position)
        {
            var tank = _world.SpawnUnit(1, position, maxHealth: 100f);
            _world.Add(tank, new SpawnOnDeath { Prefab = _wreck });
            var writer = _world.Writer(tank);
            TurretTossAuthoring.Add(writer, "tank", new TurretToss
            {
                Hull = _hull, Turret = _turret, Overkill = 1.5f, MinDistance = MinDistance, MaxDistance = MaxDistance,
                Arc = Arc, Height = 5f, Duration = 1f,
            }, new[] { _tankRound });
            return tank;
        }

        // The shot comes from the south, so the turret goes north.
        private void Shoot(Entity tank, DamageType type, float amount, float3 at)
        {
            _world.QueueDamage(new DamageEvent
            {
                Target = tank,
                Origin = at + new float3(0f, 0f, -10f),
                SourceFaction = 2,
                Profile = new DamageProfile { Amount = amount, Type = type },
            });
            _world.Tick();
        }

        private float3 NearestTurret(float3 near)
        {
            using var turrets = _world.All<TossedTurret>();
            var best = float3.zero;
            var bestDistance = float.MaxValue;
            foreach (var turret in turrets)
            {
                var position = _world.GetPosition(turret);
                var distance = math.distance(position, near);
                bestDistance = math.min(bestDistance, distance);
                best = distance <= bestDistance ? position : best;
            }

            return best;
        }

        // Prefabs carry a faction no unit has, so copies of each can be told apart.
        private int Remains(byte mark)
        {
            using var all = _world.All<Faction>();
            var count = 0;
            foreach (var entity in all)
            {
                count += _world.Get<Faction>(entity).Value == mark ? 1 : 0;
            }

            return count;
        }
    }
}
