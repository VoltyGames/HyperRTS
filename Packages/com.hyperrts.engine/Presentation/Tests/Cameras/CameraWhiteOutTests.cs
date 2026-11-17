using HyperRTS.Input.Cameras;
using HyperRTS.Presentation.Effects.Rig;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Cameras
{
    /// <summary>The screen white-out from blinding bursts: it rises at once, fades by distance and recovers.</summary>
    public class CameraWhiteOutTests
    {
        private static readonly Vector3 Focus = new(10f, 0f, 10f);

        [Test]
        public void Pulse_WhitesOutAtOnce_ThenRecovers()
        {
            var whiteOut = new CameraWhiteOut();
            whiteOut.Add(new WhiteOutPulse(Focus, 0.8f, 100f), Focus);
            Assert.AreEqual(0.8f, whiteOut.Level, 1e-4f);

            whiteOut.Advance(whiteOut.recoverySeconds * 0.25f);
            Assert.That(whiteOut.Level, Is.InRange(0.1f, 0.8f), "still fading");

            whiteOut.Advance(whiteOut.recoverySeconds * 2f);
            Assert.AreEqual(0f, whiteOut.Level);
        }

        [Test]
        public void Pulse_WeakensWithDistance_AndStopsAtItsReach()
        {
            var whiteOut = new CameraWhiteOut();
            whiteOut.Add(new WhiteOutPulse(Focus + new Vector3(50f, 0f, 0f), 1f, 100f), Focus);
            Assert.AreEqual(0.5f, whiteOut.Level, 1e-4f);

            var far = new CameraWhiteOut();
            far.Add(new WhiteOutPulse(Focus + new Vector3(150f, 0f, 0f), 1f, 100f), Focus);
            Assert.AreEqual(0f, far.Level);
        }

        [Test]
        public void TurnedOff_ShowsNothing()
        {
            var whiteOut = new CameraWhiteOut();
            whiteOut.Add(new WhiteOutPulse(Focus, 1f, 100f), Focus);

            whiteOut.enabled = false;
            whiteOut.Step(Focus, 1f / 60f);

            Assert.AreEqual(0f, whiteOut.Level);
        }
    }
}
