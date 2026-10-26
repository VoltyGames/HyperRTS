using HyperRTS.Presentation.Animation;
using HyperRTS.Simulation.Tests;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests.Animation
{
    public partial class MotionRigTests
    {
        private const float ArcRadius = 10f;

        // Drives a left-hand circle of ArcRadius, half a metre a frame.
        private void DriveLeftArc(int frames)
        {
            var center = _position + new float3(-ArcRadius, 0f, 0f);
            for (var i = 1; i <= frames; i++)
            {
                var angle = i * 0.5f / ArcRadius;
                var position = center + new float3(math.cos(angle), 0f, math.sin(angle)) * ArcRadius;
                _em.SetComponentData(_unit, LocalTransform.FromPositionRotation(position, quaternion.RotateY(-angle)));
                Update(1);
            }
        }

        [Test]
        public void SteeringWheel_TurnsByTheArcTangentOfItsDistanceTimesTheCurvature()
        {
            var wheel = Rig(new MotionRigPart
            {
                Kind = MotionPartKind.SteeringWheel, Axis = math.right(), Size = 0.5f, SteerAxis = math.down(),
                RearAxleDistance = 3f,
            });
            Update(1);

            DriveLeftArc(20);

            var steer = math.atan(3f / ArcRadius);
            Assert.AreEqual(steer, Part(0).Amount, 1e-3f);
            var expected = math.mul(quaternion.AxisAngle(math.down(), steer), quaternion.AxisAngle(math.right(), Part(0).Angle));
            MathAssert.Near(math.rotate(expected, math.forward()), math.rotate(Pose(wheel).Rotation, math.forward()), 1e-3f);
            Assert.Greater(math.rotate(Pose(wheel).Rotation, math.right()).z, 0f, "steers left: its right side swings forward");
        }

        [Test]
        public void FixedWheel_RollsButNeverSteers()
        {
            var wheel = Rig(new MotionRigPart { Kind = MotionPartKind.Wheel, Axis = math.right(), Size = 0.5f });
            Update(1);

            DriveLeftArc(20);

            Assert.AreEqual(0f, Part(0).Amount);
            MathAssert.Near(math.right(), math.rotate(Pose(wheel).Rotation, math.right()), 1e-4f);
        }
    }
}
