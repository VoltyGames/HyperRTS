using HyperRTS.Presentation.Destruction;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Tests;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests
{
    public class ToppleSystemTests
    {
        private const float Step = 0.1f;

        private World _world;
        private EntityManager _em;
        private SystemHandle _topple;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Topple Tests");
            _em = _world.EntityManager;
            _topple = _world.CreateSystem<ToppleSystem>();
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void WitnessedFall_TipsTheRenderedPoseOverTime_ThenRestsInTheFinalPose()
        {
            var remains = SpawnFelled(true, out var final);
            var child = AddChild(remains, new float3(0f, 3f, 0f));

            Update(1);
            var midway = Shown(remains);
            Assert.Less(math.abs(math.dot(midway.value, final.value)), 0.99f, "still near upright");
            Assert.AreEqual(final, _em.GetComponentData<LocalTransform>(remains).Rotation, "simulation pose untouched");
            MathAssert.Near(math.transform(_em.GetComponentData<LocalToWorld>(remains).Value, new float3(0f, 3f, 0f)),
                _em.GetComponentData<LocalToWorld>(child).Position, 1e-3f);
            Assert.IsTrue(_em.IsComponentEnabled<FallClock>(remains));

            Update((int)math.ceil(ToppleMath.Duration / Step));
            Assert.Greater(math.abs(math.dot(Shown(remains).value, final.value)), 0.9999f);
            Assert.IsFalse(_em.IsComponentEnabled<FallClock>(remains));
        }

        [Test]
        public void UnseenFall_ShowsLyingDownAtOnce()
        {
            var remains = SpawnFelled(false, out var final);
            var standing = _em.GetComponentData<LocalToWorld>(remains).Value;

            Update(1);

            Assert.AreEqual(standing, _em.GetComponentData<LocalToWorld>(remains).Value, "rendered as simulated");
            Assert.IsFalse(_em.IsComponentEnabled<FallClock>(remains));
        }

        private Entity SpawnFelled(bool witnessed, out quaternion final)
        {
            var standing = LocalTransform.FromPosition(new float3(2f, 0f, 2f));
            var topple = new Topple { Trunk = new float3(0f, 1f, 0f), Lie = 0f };
            var felling = ToppleMath.Fell(standing, topple, 64, witnessed, out var pose);
            var remains = _em.CreateEntity();
            _em.AddComponentData(remains, pose);
            _em.AddComponentData(remains, new LocalToWorld { Value = pose.ToMatrix() });
            _em.AddComponentData(remains, felling);
            final = pose.Rotation;
            return remains;
        }

        private Entity AddChild(Entity parent, float3 offset)
        {
            var child = _em.CreateEntity();
            _em.AddComponentData(child, LocalTransform.FromPosition(offset));
            _em.AddComponentData(child, new LocalToWorld { Value = float4x4.identity });
            _em.AddBuffer<Child>(parent).Add(new Child { Value = child });
            return child;
        }

        private quaternion Shown(Entity entity) => _em.GetComponentData<LocalToWorld>(entity).Rotation;

        private void Update(int frames)
        {
            for (var i = 0; i < frames; i++)
            {
                _world.SetTime(new TimeData(_world.Time.ElapsedTime + Step, Step));
                _topple.Update(_world.Unmanaged);
            }
        }
    }
}
