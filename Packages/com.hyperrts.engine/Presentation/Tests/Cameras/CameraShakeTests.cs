using HyperRTS.Input.Cameras;
using HyperRTS.Presentation.Effects.Rig;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Cameras
{
    /// <summary>The camera shake from effect bursts: it dies out, stays within its caps and stays still when turned off.</summary>
    public class CameraShakeTests
    {
        private const float Step = 1f / 60f;

        private static readonly Vector3 Focus = new(10f, 0f, 10f);

        [Test]
        public void Jolt_ShakesAtFirst_ThenFadesToNothingWithinItsDuration()
        {
            var shake = new CameraShake();
            shake.Add(new CameraJolt(Focus, 0.1f, 0f, 0f), Focus, 1f);

            var moved = 0f;
            for (var time = 0f; time < 0.1f; time += Step)
            {
                shake.Advance(Step);
                moved = Mathf.Max(moved, shake.Offset.position.magnitude);
            }

            Assert.Greater(moved, 0f);
            shake.Advance(shake.duration);
            Assert.AreEqual(Vector3.zero, shake.Offset.position);
            Assert.AreEqual(0f, Quaternion.Angle(Quaternion.identity, shake.Offset.rotation), 1e-4f);
        }

        [Test]
        public void StackedJolts_NeverPassTheCaps()
        {
            var shake = new CameraShake();
            for (var i = 0; i < 100; i++)
            {
                shake.Add(new CameraJolt(Focus, 5f, 0f, 0f), Focus, 1f);
            }

            for (var frame = 0; frame < 15; frame++)
            {
                shake.Advance(Step);
                Assert.LessOrEqual(shake.Offset.position.magnitude, shake.maxOffset + 1e-5f);
                Assert.LessOrEqual(Quaternion.Angle(Quaternion.identity, shake.Offset.rotation), shake.maxAngle + 1e-2f);
            }
        }

        [Test]
        public void TurnedOff_DropsJolts_AndNothingMoves()
        {
            var shake = new CameraShake();
            shake.Add(new CameraJolt(Focus, 5f, 0f, 0f), Focus, 1f);
            shake.Advance(Step);

            shake.enabled = false;
            shake.Step(Focus, 1f, Step);
            Assert.AreEqual(Pose.identity, shake.Offset);
            shake.Advance(Step);
            Assert.AreEqual(Vector3.zero, shake.Offset.position);
        }
    }
}
