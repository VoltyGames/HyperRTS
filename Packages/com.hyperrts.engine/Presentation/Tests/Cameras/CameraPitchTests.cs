using HyperRTS.Input.Cameras;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Cameras
{
    /// <summary>The match camera's pitch with zoom and its tilt toward the horizon at the closest zooms.</summary>
    public class CameraPitchTests
    {
        private GameObject _rig;
        private CameraController _controller;

        [SetUp]
        public void SetUp()
        {
            _rig = new GameObject("Camera", typeof(Camera));
            _rig.SetActive(false);
            _controller = _rig.AddComponent<CameraController>();
            _controller.minZoom = 14f;
            _controller.maxZoom = 80f;
            _controller.pitchAtMinZoom = 44f;
            _controller.pitchAtMaxZoom = 60f;
            _controller.pitchAtTilt = 18f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_rig);

        [Test]
        public void WithoutTilt_PitchRunsLinearlyBetweenTheZoomLimits()
        {
            Assert.AreEqual(44f, _controller.PitchAt(14f), 1e-4f);
            Assert.AreEqual(52f, _controller.PitchAt(47f), 1e-4f);
            Assert.AreEqual(60f, _controller.PitchAt(80f), 1e-4f);
            Assert.AreEqual(0f, _controller.TiltAt(14f));
        }

        [Test]
        public void Tilt_ReachesItsPitchAtTheClosestZoom_AndLeavesHigherZoomsAlone()
        {
            _controller.tiltBelowHeight = 25f;
            Assert.AreEqual(18f, _controller.PitchAt(14f), 1e-4f);
            Assert.AreEqual(1f, _controller.TiltAt(14f), 1e-4f);
            Assert.AreEqual(0f, _controller.TiltAt(25f), 1e-4f);
            Assert.AreEqual(52f, _controller.PitchAt(47f), 1e-4f);
            Assert.That(_controller.PitchAt(20f), Is.InRange(18f, 47f));
        }

        [Test]
        public void Zoom_ChangesHeightByAShare_AndStopsAtTheLimits()
        {
            Assert.AreEqual(8.5f, CameraController.ZoomedHeight(10f, 1f, 0.15f, 4f, 80f), 1e-4f);
            Assert.AreEqual(11.5f, CameraController.ZoomedHeight(10f, -1f, 0.15f, 4f, 80f), 1e-4f);
            Assert.AreEqual(4f, CameraController.ZoomedHeight(4.2f, 3f, 0.15f, 4f, 80f), 1e-4f);
            Assert.AreEqual(80f, CameraController.ZoomedHeight(79f, -1f, 0.15f, 4f, 80f), 1e-4f);
        }

        [Test]
        public void Clearance_LiftsTheCameraOnlyWhenTheTerrainIsTooClose()
        {
            Assert.AreEqual(11.5f, CameraController.ClearedHeight(5f, 10f, 1.5f), 1e-4f);
            Assert.AreEqual(20f, CameraController.ClearedHeight(20f, 10f, 1.5f), 1e-4f);
        }
    }
}
