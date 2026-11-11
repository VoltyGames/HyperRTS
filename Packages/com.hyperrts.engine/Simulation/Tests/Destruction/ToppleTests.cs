using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Geometry;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Death;
using HyperRTS.Simulation.Destruction;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Destruction
{
    /// <summary>Felled trees: the heading they fall along, their remains pose, and the fall clients show.</summary>
    public class ToppleTests : SimulationFixture
    {
        private const float PoseTolerance = 0.01f;

        [Test]
        public void Quantize_RoundTripsEveryHeading_FallingAwayFromTheSource()
        {
            var tree = new float3(3f, 0f, -2f);
            for (var fall = 0; fall < HeadingByte.Steps; fall++)
            {
                var source = tree - HeadingByte.Direction((byte)fall) * 7f;
                Assert.AreEqual(fall, ToppleMath.Quantize(source, tree), $"heading {fall}");
            }
        }

        [Test]
        public void RemainsPose_PointsTheCrownAlongTheFall()
        {
            var standing = LocalTransform.FromPositionRotationScale(new float3(1f, 2f, 3f), quaternion.RotateY(1f), 1.5f);
            const float lie = 0.7f;

            var pose = ToppleMath.RemainsPose(standing, 64, lie);

            var canopy = math.rotate(pose.Rotation, new float3(math.sin(lie), 0f, math.cos(lie)));
            MathAssert.Near(new float3(1f, 0f, 0f), canopy, PoseTolerance);
            MathAssert.Near(standing.Position, pose.Position, PoseTolerance);
            Assert.AreEqual(1.5f, pose.Scale);
        }

        [Test]
        public void Fell_StartsWithTheTrunkWhereItStood_AndEndsAtTheRemainsPose()
        {
            var standing = LocalTransform.FromPositionRotation(float3.zero, quaternion.RotateY(0.4f)).ApplyScale(2f);
            var topple = new Topple { Trunk = math.normalize(new float3(0.1f, 1f, 0f)), Height = 7f, Lie = 0.3f };

            var toppleFall = ToppleMath.Fell(standing, topple, 200, true, out var pose);

            var canopy = new float3(math.sin(topple.Lie), 0f, math.cos(topple.Lie));
            var upright = ToppleMath.Pose(toppleFall, pose.Rotation, 0f);
            MathAssert.Near(math.rotate(standing.Rotation, topple.Trunk), math.rotate(upright, canopy), PoseTolerance);
            var down = ToppleMath.Pose(toppleFall, pose.Rotation, 1f);
            MathAssert.Near(HeadingByte.Direction(200), math.rotate(down, canopy), PoseTolerance);
            MathAssert.Near(math.rotate(pose.Rotation, canopy), HeadingByte.Direction(200), PoseTolerance);
            MathAssert.Near(HeadingByte.Direction(200), toppleFall.CanopyDirection, PoseTolerance);
            Assert.AreEqual(14f, toppleFall.Height, 1e-4f, "scaled with the tree");
        }

        [Test]
        public void LethalSplash_FellsTheTreeAwayFromTheBlast_InThePoseClientsDerive()
        {
            var remains = World.MakePrefab(World.EntityManager.CreateEntity(typeof(LocalTransform)));
            var tree = SpawnTree(new float3(5f, 0f, 0f), remains, out var standing, out var topple);

            World.QueueDamage(new DamageEvent { Position = float3.zero, Layers = WeaponTargets.Surface, Profile = new DamageProfile { Amount = 999f, Radius = 8f, EdgeFactor = 1f } });
            World.Tick();

            Assert.IsFalse(World.EntityManager.Exists(tree));
            var fall = ToppleMath.Quantize(float3.zero, standing.Position);
            MathAssert.Near(new float3(1f, 0f, 0f), HeadingByte.Direction(fall), PoseTolerance);
            var expected = ToppleMath.Fell(standing, topple, fall, true, out var pose);
            var (shown, toppleFall) = FellRemains();
            MathAssert.Near(pose.Position, shown.Position, PoseTolerance);
            Assert.Greater(math.abs(math.dot(pose.Rotation.value, shown.Rotation.value)), 1f - 1e-3f, "same pose");
            Assert.Greater(math.abs(math.dot(expected.Tilt.value, toppleFall.Tilt.value)), 1f - 1e-3f, "same fall");
        }

        private Entity SpawnTree(float3 position, Entity remains, out LocalTransform standing, out Topple topple)
        {
            var entity = World.EntityManager.CreateEntity();
            standing = LocalTransform.FromPositionRotation(position, quaternion.RotateY(0.5f));
            topple = new Topple { Trunk = new float3(0f, 1f, 0f), Height = 12f, Lie = 0f, CanopyRadius = 3f };
            World.Add(entity, standing);
            var writer = World.Writer(entity);
            DestructibleAuthoring.AddScenery(writer, 50f, 0, remains, float2.zero,
                SceneryId.At(position, standing.Rotation));
            ToppleAuthoring.AddTopple(writer, topple.Trunk, topple.Height, topple.Lie, topple.CanopyRadius);
            return entity;
        }

        private (LocalTransform, ToppleFall) FellRemains()
        {
            using var query = World.Query(ComponentType.ReadOnly<ToppleFall>(),
                ComponentType.ReadOnly<LocalTransform>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(1, entities.Length, "one felled remains");
            var entity = entities[0];
            return (World.Get<LocalTransform>(entity),
                World.Get<ToppleFall>(entity));
        }
    }
}
