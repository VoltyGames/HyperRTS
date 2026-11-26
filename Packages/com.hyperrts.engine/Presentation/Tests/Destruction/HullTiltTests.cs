using HyperRTS.Presentation.Destruction;
using HyperRTS.Simulation.Navigation.Areas;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests.Destruction
{
    /// <summary>A wreck is laid once along the slope under its hull, by its root's matrix, and level ground leaves it level.</summary>
    public class HullTiltTests
    {
        private PresentationWorldKit _kit;
        private EntityManager _em;
        private SystemHandle _system;

        [SetUp]
        public void SetUp()
        {
            _kit = new PresentationWorldKit("Hull Tilt Tests");
            _em = _kit.EntityManager;
            _kit.AddGround();
            _system = _kit.World.CreateSystem<HullTiltSystem>();
        }

        [TearDown]
        public void TearDown() => _kit.Dispose();

        [Test]
        public void WreckFacingUphill_LiesNoseUpAlongTheSlope_AndIsLaidOnce()
        {
            RaiseGroundAlongX();
            var wreck = Wreck(quaternion.RotateY(math.PI * 0.5f));

            _system.Update(_kit.World.Unmanaged);

            var slope = math.atan(0.5f);
            Assert.AreEqual(slope, _em.GetComponentData<HullTilt>(wreck).Value.x, 1e-3f, "pitch");
            Assert.AreEqual(0f, _em.GetComponentData<HullTilt>(wreck).Value.y, 1e-3f, "roll");
            Assert.IsFalse(_em.IsComponentEnabled<HullTilt>(wreck));
            var nose = math.rotate(_em.GetComponentData<PostTransformMatrix>(wreck).Value, math.forward());
            Assert.AreEqual(math.sin(slope), nose.y, 1e-3f, "the model's forward rises with the ground");
        }

        [Test]
        public void WreckOnLevelGround_StaysLevel()
        {
            var wreck = Wreck(quaternion.identity);

            _system.Update(_kit.World.Unmanaged);

            var up = math.rotate(_em.GetComponentData<PostTransformMatrix>(wreck).Value, math.up());
            Assert.AreEqual(1f, up.y, 1e-5f);
        }

        [Test]
        public void WreckTheMapLeanedAlongItsSlope_IsNotLeanedAgain()
        {
            RaiseGroundAlongX();
            var leaned = math.mul(quaternion.RotateY(math.PI * 0.5f), quaternion.RotateX(-math.atan(0.5f)));
            var wreck = Wreck(leaned);

            _system.Update(_kit.World.Unmanaged);

            var up = math.rotate(_em.GetComponentData<PostTransformMatrix>(wreck).Value, math.up());
            Assert.AreEqual(1f, up.y, 1e-3f);
        }

        // Half a metre a metre along x, on 2 m samples.
        private void RaiseGroundAlongX()
        {
            var size = new int2(33, 33);
            var heights = new NativeArray<float>(size.x * size.y, Allocator.Temp);
            for (var i = 0; i < heights.Length; i++)
            {
                heights[i] = i % size.x;
            }

            _kit.SetTerrain(heights, size);
        }

        // A wreck as its prefab bakes: a blocker with the tilt and matrix the baking system adds.
        private Entity Wreck(quaternion rotation)
        {
            var wreck = _em.CreateEntity();
            _em.AddComponentData(wreck, LocalTransform.FromPositionRotation(new float3(20f, 10f, 20f), rotation));
            _em.AddComponentData(wreck, new Blocker { Radius = 1.5f, HullLength = 8f });
            _em.AddComponent<HullTilt>(wreck);
            _em.AddComponentData(wreck, new PostTransformMatrix { Value = float4x4.identity });
            return wreck;
        }
    }
}
