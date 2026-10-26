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

namespace HyperRTS.Presentation.Tests.Animation
{
    /// <summary>The motion rig on a hand-made unit: turrets, recoil, wheels and steering, rotors, slope tilt and the body's suspension sway.</summary>
    public partial class MotionRigTests
    {
        private const float Step = 0.1f;
        private const float SpringFrame = 1f / 60f;

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
        public void Turret_EasesToThePoseYaw()
        {
            var part = Rig(new MotionRigPart { Kind = MotionPartKind.Turret, Axis = math.up() });
            SetPose(new WeaponPose { Yaw = 64 });

            Update(1);
            Assert.That(Part(0).Angle, Is.InRange(0.1f, math.radians(90f) - 0.01f), "on its way after a frame");

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

        [Test]
        public void Suspension_SquatsDivesAndRolls_NoMoreThanTheLimit()
        {
            var springs = Springs();

            Assert.Greater(springs.Target(math.forward() * 2f, math.forward(), math.right()).x, 0f, "squats nose up");
            Assert.Less(springs.Target(math.back() * 2f, math.forward(), math.right()).x, 0f, "dives braking");
            var turn = springs.Target(math.right() * 3f, math.forward(), math.right());
            Assert.Greater(turn.y, 0f, "a right turn rolls the right side up");
            Assert.AreEqual(0f, turn.x, 1e-6f);
            Assert.AreEqual(-springs.MaxSway, springs.Target(math.back() * 40f, math.forward(), math.right()).x, 1e-6f);
        }

        [Test]
        public void Suspension_SpringOvershootsThenSettlesOnTheTarget()
        {
            var springs = Springs();
            var target = new float2(0.02f, -0.01f);
            var peak = 0f;
            for (var i = 0; i < 300; i++)
            {
                springs.Spring(target, SpringFrame);
                peak = math.max(peak, springs.Sway.x);
            }

            Assert.Greater(peak, target.x, "bounces past on underdamped springs");
            MathAssert.Near(new float3(target, 0f), new float3(springs.Sway, 0f), 1e-4f);
            Assert.Less(math.length(springs.Rate), 1e-3f);
        }

        [Test]
        public void Suspension_ShotForwardRocksTheNoseUp_ThenComesBackLevel()
        {
            var springs = Springs();
            springs.Recoil(0f);
            springs.Spring(float2.zero, SpringFrame * 6f);
            Assert.Greater(springs.Sway.x, 0f);
            Assert.AreEqual(0f, springs.Sway.y, 1e-6f);

            for (var i = 0; i < 300; i++)
            {
                springs.Spring(float2.zero, SpringFrame);
            }

            Assert.Less(math.length(springs.Sway), 1e-4f);
        }

        [Test]
        public void Suspension_LongHitchStaysStable()
        {
            var springs = Springs();

            springs.Spring(new float2(0.03f, 0f), 0.5f);

            Assert.LessOrEqual(math.abs(springs.Sway.x), springs.MaxSway);
            Assert.IsFalse(math.any(math.isnan(springs.Rate)));
        }

        [Test]
        public void Suspension_ZeroSpringsNeverSwayOrKick()
        {
            var aircraft = default(Suspension);
            aircraft.Recoil(0f);
            aircraft.Spring(aircraft.Target(math.forward() * 5f, math.forward(), math.right()), SpringFrame);

            Assert.AreEqual(float2.zero, aircraft.Sway);
            Assert.AreEqual(float2.zero, aircraft.Rate);
        }

        private static Suspension Springs() => new()
        {
            Frequency = 1.5f,
            Damping = 0.45f,
            PerAcceleration = math.radians(0.6f),
            MaxSway = math.radians(3f),
            RecoilRate = math.radians(12f),
        };

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
