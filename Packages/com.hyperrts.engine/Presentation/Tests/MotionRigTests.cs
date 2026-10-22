using HyperRTS.Presentation.Animation;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Tests;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests
{
    public class MotionRigTests
    {
        private const float Step = 0.1f;

        private World _world;
        private EntityManager _em;
        private SystemHandle _system;
        private Entity _unit;
        private float3 _position;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Motion Rig Tests");
            _em = _world.EntityManager;
            _system = _world.CreateSystem<MotionRigSystem>();
            _position = new float3(10f, 0f, 10f);
        }

        [TearDown]
        public void TearDown()
        {
            using (var query = _em.CreateEntityQuery(typeof(TerrainHeight)))
            {
                if (!query.IsEmpty)
                {
                    query.GetSingleton<TerrainHeight>().Blob.Dispose();
                }
            }

            _world.Dispose();
        }

        [Test]
        public void Turret_TraversesToThePoseYaw_AtItsRate()
        {
            var part = Rig(new MotionRigPart { Kind = MotionPartKind.Turret, Axis = math.up(), Rate = math.radians(90f) });
            SetPose(new WeaponPose { Yaw = 64 });

            Update(5);
            Assert.AreEqual(math.radians(45f), Part(0).Angle, 1e-4f, "half way after half a second");

            Update(10);
            Assert.AreEqual(math.radians(90f), Part(0).Angle, 1e-4f);
            MathAssert.Near(math.right(), math.rotate(Pose(part).Rotation, math.forward()), 1e-4f);
        }

        [Test]
        public void Recoil_KicksOnAShot_ThenReturnsToRest()
        {
            var rest = new float3(0f, 1f, 2f);
            var part = Rig(new MotionRigPart
            {
                Kind = MotionPartKind.Recoil, Axis = math.back(), Size = 0.4f, Time = 0.5f, RestPosition = rest,
            });
            Update(1);
            MathAssert.Near(rest, Pose(part).Position);

            SetPose(new WeaponPose { Shots = 1 });
            Update(1);
            MathAssert.Near(rest + math.back() * 0.4f, Pose(part).Position);

            Update(5);
            MathAssert.Near(rest, Pose(part).Position);
        }

        [Test]
        public void Wheel_TurnsByDistanceOverRadius()
        {
            Rig(new MotionRigPart { Kind = MotionPartKind.Wheel, Axis = math.right(), Size = 0.5f });
            Update(1);

            MoveTo(_position + math.forward() * 0.6f);
            Assert.AreEqual(1.2f, Part(0).Angle, 1e-4f, "forward rolls forward");

            MoveTo(_position + math.back() * 0.3f);
            Assert.AreEqual(-0.6f, Part(0).Angle, 1e-4f, "reversing rolls back");
        }

        [Test]
        public void Rotor_SpinsUpInFlight_AndStopsWhenDocked()
        {
            Rig(new MotionRigPart { Kind = MotionPartKind.Rotor, Axis = math.up(), Rate = 10f, Time = 1f });
            _em.AddComponentData(_unit, new Flight { Altitude = 10f });
            _em.AddComponent<Docked>(_unit);
            _em.SetComponentEnabled<Docked>(_unit, false);

            Update(10);
            Assert.AreEqual(1f, Part(0).Amount, 1e-4f);
            Assert.AreEqual(1f, _em.GetComponentData<RotorSpin>(_unit).Amount, 1e-4f, "the sound follows the blades");

            _em.SetComponentEnabled<Docked>(_unit, true);
            Update(10);
            var stopped = Part(0).Angle;
            Update(3);
            Assert.AreEqual(0f, Part(0).Amount, 1e-4f);
            Assert.AreEqual(stopped, Part(0).Angle, 1e-6f);
            Assert.AreEqual(0f, _em.GetComponentData<RotorSpin>(_unit).Amount, 1e-4f);
        }

        [Test]
        public void OnlyRigsWithARotor_GetARotorSpin()
        {
            Rig(new MotionRigPart { Kind = MotionPartKind.Wheel, Axis = math.right(), Size = 0.5f });
            Assert.IsFalse(_em.HasComponent<RotorSpin>(_unit));

            Rig(new MotionRigPart { Kind = MotionPartKind.Rotor, Axis = math.up(), Rate = 10f, Time = 1f });
            Assert.IsTrue(_em.HasComponent<RotorSpin>(_unit));
        }

        [Test]
        public void Tilt_FollowsASlope()
        {
            const float grade = 0.1f;
            AddSlope(grade);
            Rig(new MotionRigPart { Kind = MotionPartKind.Wheel, Axis = math.right(), Size = 0.5f });
            var hull = _em.CreateEntity();
            _em.AddComponentData(hull, LocalTransform.Identity);
            var rig = _em.GetComponentData<MotionRig>(_unit);
            rig.TiltRoot = hull;
            rig.Footprint = new float2(6f, 3f);
            _em.SetComponentData(_unit, rig);

            Update(30);

            var nose = math.rotate(Pose(hull).Rotation, math.forward());
            Assert.AreEqual(math.atan(grade), math.asin(nose.y), 1e-3f, "nose up the slope");
            Assert.AreEqual(0f, _em.GetComponentData<MotionRig>(_unit).Tilt.y, 1e-4f, "no roll across it");
        }

        private Entity Rig(MotionRigPart part)
        {
            var child = _em.CreateEntity();
            _em.AddComponentData(child, LocalTransform.FromPositionRotation(part.RestPosition, quaternion.identity));
            part.Part = child;
            part.RestRotation = quaternion.identity;

            _unit = _em.CreateEntity();
            _em.AddComponentData(_unit, LocalTransform.FromPosition(_position));
            _em.AddBuffer<WeaponPose>(_unit).Add(default);
            var writer = new EntityManagerWriter(_em, _unit);
            MotionRigAuthoring.AddRig(ref writer, new MotionRig { TiltFrame = quaternion.identity, TiltRest = quaternion.identity },
                new[] { part });
            return child;
        }

        // Rises along +z, so a unit facing +z climbs it.
        private void AddSlope(float grade)
        {
            var size = new int2(16, 16);
            var heights = new NativeArray<float>(size.x * size.y, Allocator.Temp);
            for (var i = 0; i < heights.Length; i++)
            {
                heights[i] = grade * (i / size.x) * 2f;
            }

            var terrain = TerrainHeight.Create(heights, size, float2.zero, 2f, Allocator.Persistent);
            heights.Dispose();
            _em.AddComponentData(_em.CreateEntity(), terrain);
        }

        private void SetPose(WeaponPose pose)
        {
            var poses = _em.GetBuffer<WeaponPose>(_unit);
            poses[0] = pose;
        }

        private void MoveTo(float3 position)
        {
            _em.SetComponentData(_unit, LocalTransform.FromPosition(position));
            Update(1);
        }

        private MotionRigPart Part(int index) => _em.GetBuffer<MotionRigPart>(_unit)[index];

        private LocalTransform Pose(Entity part) => _em.GetComponentData<LocalTransform>(part);

        private void Update(int frames)
        {
            for (var i = 0; i < frames; i++)
            {
                _world.SetTime(new TimeData(_world.Time.ElapsedTime + Step, Step));
                _system.Update(_world.Unmanaged);
                _em.CompleteAllTrackedJobs();
            }
        }
    }
}
