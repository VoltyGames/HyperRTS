using HyperRTS.Presentation.Destruction;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Tests;
using NUnit.Framework;
using Unity.Collections;
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
        public void WitnessedFall_TurnsTheModelOverTime_ThenRestsItInTheFinalPose()
        {
            var remains = SpawnFelled(true, out var final);
            var model = AddModel(remains, new float3(0f, 3f, 0f));

            Update(1);
            var midway = math.mul(final, Pose(model).Rotation);
            Assert.Less(math.abs(math.dot(midway.value, final.value)), 0.99f, "still near upright");
            Assert.AreEqual(final, _em.GetComponentData<LocalTransform>(remains).Rotation, "simulation pose untouched");
            MathAssert.Near(math.rotate(Pose(model).Rotation, new float3(0f, 3f, 0f)), Pose(model).Position, 1e-3f);
            Assert.IsTrue(_em.IsComponentEnabled<FallClock>(remains));

            Update((int)math.ceil(8f / Step));
            Assert.AreEqual(LocalTransform.FromPosition(new float3(0f, 3f, 0f)), Pose(model), "back at rest exactly");
            Assert.IsFalse(_em.IsComponentEnabled<FallClock>(remains));
        }

        [Test]
        public void UnseenFall_ShowsLyingDownAtOnce()
        {
            var remains = SpawnFelled(false, out _);
            var model = AddModel(remains, new float3(0f, 3f, 0f));

            Update(1);

            Assert.AreEqual(LocalTransform.FromPosition(new float3(0f, 3f, 0f)), Pose(model), "rendered as simulated");
            Assert.IsFalse(_em.IsComponentEnabled<FallClock>(remains));
        }

        private Entity SpawnFelled(bool witnessed, out quaternion final)
        {
            var standing = LocalTransform.FromPosition(new float3(2f, 0f, 2f));
            var topple = new Topple { Trunk = new float3(0f, 1f, 0f), Height = 15f, Lie = 0f };
            var toppleFall = ToppleMath.Fell(standing, topple, 64, witnessed, out var pose);
            var remains = _em.CreateEntity();
            _em.AddComponentData(remains, pose);
            _em.AddComponentData(remains, toppleFall);
            final = pose.Rotation;
            return remains;
        }

        /// <summary>A model under the remains, linked as a fresh instance is, before any Child buffer exists.</summary>
        private Entity AddModel(Entity remains, float3 offset)
        {
            var model = _em.CreateEntity();
            _em.AddComponentData(model, LocalTransform.FromPosition(offset));
            _em.AddComponentData(model, new Parent { Value = remains });
            _em.AddBuffer<LinkedEntityGroup>(remains).AddRange(new NativeArray<LinkedEntityGroup>(
                new LinkedEntityGroup[] { remains, model }, Allocator.Temp));
            return model;
        }

        private LocalTransform Pose(Entity entity) => _em.GetComponentData<LocalTransform>(entity);

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
