using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Trees falling away from what felled them, and the remains pose servers and clients share.</summary>
    public class ToppleTests
    {
        private const float Tolerance = 1e-3f;

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
        public void Quantize_RoundTripsEveryHeading()
        {
            var tree = new float3(3f, 0f, -2f);
            for (var fall = 0; fall < ToppleMath.Headings; fall++)
            {
                var source = tree - ToppleMath.Direction((byte)fall) * 7f;
                Assert.AreEqual(fall, ToppleMath.Quantize(source, tree), $"heading {fall}");
            }
        }

        [Test]
        public void Quantize_FallsAwayFromTheSource()
        {
            var fall = ToppleMath.Quantize(new float3(0f, 5f, 0f), new float3(4f, 0f, 4f));

            var direction = ToppleMath.Direction(fall);
            Assert.AreEqual(math.SQRT2 / 2f, direction.x, 0.02f);
            Assert.AreEqual(math.SQRT2 / 2f, direction.z, 0.02f);
        }

        [Test]
        public void RemainsPose_PointsTheCrownAlongTheFall()
        {
            var standing = LocalTransform.FromPositionRotationScale(new float3(1f, 2f, 3f), quaternion.RotateY(1f), 1.5f);
            const float lie = 0.7f;

            var pose = ToppleMath.RemainsPose(standing, 64, lie);

            var crown = math.rotate(pose.Rotation, new float3(math.sin(lie), 0f, math.cos(lie)));
            AssertNear(new float3(1f, 0f, 0f), crown);
            AssertNear(standing.Position, pose.Position);
            Assert.AreEqual(1.5f, pose.Scale);
        }

        [Test]
        public void Fell_StartsWithTheTrunkWhereItStood_AndEndsAtTheRemainsPose()
        {
            var standing = LocalTransform.FromPositionRotation(float3.zero, quaternion.RotateY(0.4f));
            var topple = new Topple { Trunk = math.normalize(new float3(0.1f, 1f, 0f)), Lie = 0.3f };

            var felling = ToppleMath.Fell(standing, topple, 200, true, out var pose);

            var crown = new float3(math.sin(topple.Lie), 0f, math.cos(topple.Lie));
            AssertNear(math.rotate(standing.Rotation, topple.Trunk), math.rotate(ToppleMath.Pose(felling, 0f), crown));
            AssertNear(ToppleMath.Direction(200), math.rotate(ToppleMath.Pose(felling, ToppleMath.Duration), crown));
            AssertNear(math.rotate(pose.Rotation, crown), ToppleMath.Direction(200));
        }

        [Test]
        public void Progress_EasesIn_ThenBouncesBelowTheGround_AndSettles()
        {
            Assert.AreEqual(0f, ToppleMath.Progress(0f));
            Assert.Less(ToppleMath.Progress(ToppleMath.FallTime * 0.5f), 0.5f, "eases in");
            Assert.Less(ToppleMath.Progress(ToppleMath.FallTime + 0.05f), 1f, "bounces back up off the ground");
            Assert.AreEqual(1f, ToppleMath.Progress(ToppleMath.Duration));
        }

        [Test]
        public void LethalSplash_FellsTheTreeAwayFromTheBlast_InThePoseClientsDerive()
        {
            var remains = _world.MakePrefab(_world.EntityManager.CreateEntity(typeof(LocalTransform)));
            var tree = SpawnTree(new float3(5f, 0f, 0f), remains, out var standing, out var topple);

            _world.QueueDamage(new DamageEvent { Position = float3.zero, Amount = 999f, Radius = 8f, EdgeFactor = 1f });
            _world.Tick();

            Assert.IsFalse(_world.EntityManager.Exists(tree));
            var fall = ToppleMath.Quantize(float3.zero, standing.Position);
            AssertNear(new float3(1f, 0f, 0f), ToppleMath.Direction(fall));
            var expected = ToppleMath.Fell(standing, topple, fall, true, out var pose);
            var (shown, felling) = FellRemains();
            AssertNear(pose.Position, shown.Position);
            Assert.Greater(math.abs(math.dot(pose.Rotation.value, shown.Rotation.value)), 1f - Tolerance, "same pose");
            Assert.Greater(math.abs(math.dot(expected.Tilt.value, felling.Tilt.value)), 1f - Tolerance, "same fall");
        }

        [Test]
        public void DirectHit_FellsTheTreeAwayFromTheShooter()
        {
            var remains = _world.MakePrefab(_world.EntityManager.CreateEntity(typeof(LocalTransform)));
            var tree = SpawnTree(new float3(5f, 0f, 0f), remains, out _, out _);

            _world.QueueDamage(new DamageEvent { Target = tree, Origin = new float3(5f, 0f, -10f), Amount = 999f });
            _world.Tick();

            var (shown, _) = FellRemains();
            AssertNear(new float3(0f, 0f, 1f), math.rotate(shown.Rotation, new float3(0f, 0f, 1f)));
        }

        private Entity SpawnTree(float3 position, Entity remains, out LocalTransform standing, out Topple topple)
        {
            var entity = _world.EntityManager.CreateEntity();
            standing = LocalTransform.FromPositionRotation(position, quaternion.RotateY(0.5f));
            topple = new Topple { Trunk = new float3(0f, 1f, 0f), Lie = 0f };
            _world.EntityManager.AddComponentData(entity, standing);
            var writer = new EntityManagerWriter(_world.EntityManager, entity);
            DestructibleSetup.AddScenery(ref writer, 50f, 0, remains, float2.zero,
                SceneryId.At(position, standing.Rotation));
            DestructibleSetup.AddTopple(ref writer, topple.Trunk, topple.Lie);
            return entity;
        }

        private (LocalTransform, Felling) FellRemains()
        {
            using var query = _world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Felling>(),
                ComponentType.ReadOnly<LocalTransform>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(1, entities.Length, "one felled remains");
            var entity = entities[0];
            return (_world.EntityManager.GetComponentData<LocalTransform>(entity),
                _world.EntityManager.GetComponentData<Felling>(entity));
        }

        private static void AssertNear(float3 expected, float3 actual) =>
            Assert.Less(math.distance(expected, actual), 0.01f, $"expected {expected}, was {actual}");
    }
}
