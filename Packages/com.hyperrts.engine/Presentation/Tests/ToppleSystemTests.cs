using HyperRTS.Presentation.Destruction;
using HyperRTS.Simulation.Combat;
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
        public void WitnessedFall_TipsOverTime_ThenRestsInTheFinalPose()
        {
            var remains = SpawnFelled(true, out var final);

            Update(1);
            var midway = _em.GetComponentData<LocalTransform>(remains).Rotation;
            Assert.Less(math.abs(math.dot(midway.value, final.value)), 0.99f, "still near upright");
            Assert.IsTrue(_em.IsComponentEnabled<Felling>(remains));

            Update((int)math.ceil(ToppleMath.Duration / Step));
            Assert.AreEqual(final, _em.GetComponentData<LocalTransform>(remains).Rotation);
            Assert.IsFalse(_em.IsComponentEnabled<Felling>(remains));
        }

        [Test]
        public void UnseenFall_ShowsLyingDownAtOnce()
        {
            var remains = SpawnFelled(false, out var final);

            Update(1);

            Assert.AreEqual(final, _em.GetComponentData<LocalTransform>(remains).Rotation);
            Assert.IsFalse(_em.IsComponentEnabled<Felling>(remains));
        }

        private Entity SpawnFelled(bool witnessed, out quaternion final)
        {
            var standing = LocalTransform.FromPosition(new float3(2f, 0f, 2f));
            var topple = new Topple { Trunk = new float3(0f, 1f, 0f), Lie = 0f };
            var felling = ToppleMath.Fell(standing, topple, 64, witnessed, out var pose);
            var remains = _em.CreateEntity();
            _em.AddComponentData(remains, pose);
            _em.AddComponentData(remains, felling);
            final = pose.Rotation;
            return remains;
        }

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
