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
            _controller.zoom = new CameraZoom { fraction = 0.15f, min = 4f, max = 80f, pitchAtMin = 44f, pitchAtMax = 60f };
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_rig);

        [Test]
        public void Pitch_RunsLinearlyBetweenTheZoomLimits()
        {
            var zoom = new CameraZoom { min = 14f, max = 80f, pitchAtMin = 44f, pitchAtMax = 60f };
            Assert.AreEqual(44f, zoom.PitchAt(14f), 1e-4f);
            Assert.AreEqual(52f, zoom.PitchAt(47f), 1e-4f);
            Assert.AreEqual(60f, zoom.PitchAt(80f), 1e-4f);
            Assert.AreEqual(44f, zoom.PitchAt(4f), 1e-4f);
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
            Assert.AreEqual(8.5f, _controller.zoom.Zoomed(10f, 1f), 1e-4f);
            Assert.AreEqual(11.5f, _controller.zoom.Zoomed(10f, -1f), 1e-4f);
            Assert.AreEqual(4f, _controller.zoom.Zoomed(4.2f, 3f), 1e-4f);
            Assert.AreEqual(80f, _controller.zoom.Zoomed(79f, -1f), 1e-4f);
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
            var fade = new CameraTreeFade { fullBelowHeight = 6f, noneAboveHeight = 14f };
            Assert.AreEqual(1f, fade.Amount(4f), 1e-4f);
            Assert.AreEqual(0.5f, fade.Amount(10f), 1e-4f);
            Assert.AreEqual(0f, fade.Amount(30f), 1e-4f);
        }
    }
}
