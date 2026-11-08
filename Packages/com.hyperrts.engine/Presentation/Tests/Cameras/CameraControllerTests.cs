using HyperRTS.Input.Cameras;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Cameras
{
    /// <summary>The match camera's pitch and pose with zoom, its zoom steps, terrain clearance and tree fade.</summary>
    public class CameraControllerTests
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
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_rig);

        [Test]
        public void Pitch_RunsLinearlyBetweenTheZoomLimits()
        {
            Assert.AreEqual(44f, _controller.PitchAt(14f), 1e-4f);
            Assert.AreEqual(52f, _controller.PitchAt(47f), 1e-4f);
            Assert.AreEqual(60f, _controller.PitchAt(80f), 1e-4f);
            Assert.AreEqual(44f, _controller.PitchAt(4f), 1e-4f);
        }

        [Test]
        public void View_SitsAtTheHeightAboveTheFocus_AndLooksAtIt()
        {
            var focus = new Vector3(5f, 2f, -3f);
            var view = CameraController.View(focus, 20f, 45f, 90f);
            Assert.AreEqual(22f, view.position.y, 1e-3f);
            var back = view.position + view.rotation * Vector3.forward * (20f * Mathf.Sqrt(2f));
            Assert.Less(Vector3.Distance(focus, back), 1e-3f);
            Assert.Less(view.position.x, focus.x, "A yaw of 90 looks along +x, so the camera stands on the -x side.");
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

        [Test]
        public void TreeFade_RunsFromFullAtTheCloseZoomToNoneAtMidZoom()
        {
            Assert.AreEqual(1f, CameraController.TreeFadeAmount(4f, 6f, 14f), 1e-4f);
            Assert.AreEqual(0.5f, CameraController.TreeFadeAmount(10f, 6f, 14f), 1e-4f);
            Assert.AreEqual(0f, CameraController.TreeFadeAmount(30f, 6f, 14f), 1e-4f);
        }
    }
}
