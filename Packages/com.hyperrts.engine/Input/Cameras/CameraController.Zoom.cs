using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Navigation.Heights;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Input.Cameras
{
    /// <summary>The camera's zoom height, the pitch that goes with it and its clearance over the terrain.</summary>
    public partial class CameraController
    {
        [Header("Zoom")]
        [Tooltip("Share of the current height each mouse-scroll notch changes, so close zooms move in small steps.")]
        [Range(0.01f, 0.5f)]
        public float zoomFraction = 0.15f;

        [Tooltip("Closest zoom (minimum camera height above the ground at the focus).")]
        public float minZoom = 10f;

        [Tooltip("Farthest zoom (maximum camera height above the ground at the focus).")]
        public float maxZoom = 50f;

        [Tooltip("Seconds the camera takes to ease to a new zoom height; 0 jumps at once.")]
        [Min(0f)]
        public float zoomSmoothTime = 0.12f;

        [Tooltip("Degrees down from the horizon at the closest zoom.")]
        [Range(1f, 90f)]
        public float pitchAtMinZoom = 44f;

        [Tooltip("Degrees down from the horizon at the farthest zoom; pitch runs linearly with height in between.")]
        [Range(1f, 90f)]
        public float pitchAtMaxZoom = 60f;

        [Header("Terrain")]
        [Tooltip("Seconds the focus height takes to follow the terrain while panning, so relief doesn't bob the view.")]
        [Min(0f)]
        public float groundSmoothTime = 0.3f;

        [Tooltip("Least height of the camera above the terrain under it, so hills never clip the view.")]
        [Min(0f)]
        public float terrainClearance = 1.5f;

        private readonly LiveSingleton<TerrainHeight> _terrain = new();

        /// <summary>Pitch in degrees down from the horizon at <paramref name="height"/>, linear between the zoom limits.</summary>
        public float PitchAt(float height) =>
            Mathf.Lerp(pitchAtMinZoom, pitchAtMaxZoom, Mathf.InverseLerp(minZoom, maxZoom, height));

        /// <summary>Where this camera sits to look at <paramref name="focus"/> from <paramref name="height"/> above it, turned to <paramref name="yaw"/>.</summary>
        public Pose ViewAt(Vector3 focus, float height, float yaw) => View(focus, height, PitchAt(height), yaw);

        /// <summary>The camera pose that looks down at <paramref name="pitch"/> onto <paramref name="focus"/> from <paramref name="height"/> above it.</summary>
        public static Pose View(Vector3 focus, float height, float pitch, float yaw)
        {
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            var distance = height / Mathf.Sin(pitch * Mathf.Deg2Rad);
            return new Pose(focus - rotation * Vector3.forward * distance, rotation);
        }

        /// <summary>The height after one scroll notch: scrolling up zooms in by <paramref name="fraction"/> of it, within the limits.</summary>
        public static float ZoomedHeight(float height, float scroll, float fraction, float min, float max) =>
            Mathf.Clamp(height * (1f - Mathf.Sign(scroll) * fraction), min, max);

        /// <summary>Lifts a camera height to <paramref name="clearance"/> above the terrain beneath it.</summary>
        public static float ClearedHeight(float cameraY, float terrainY, float clearance) =>
            Mathf.Max(cameraY, terrainY + clearance);

        /// <summary>The terrain height under <paramref name="point"/>; flat y = 0 before a world or terrain exists.</summary>
        private float GroundAt(Vector3 point)
        {
            _terrain.TryGetInDefaultWorld(out var terrain);
            return terrain.Height(new float2(point.x, point.z));
        }

        /// <summary>The ground height under <paramref name="focus"/>, eased from the last one.</summary>
        private float SmoothedGround(Vector3 focus) =>
            Mathf.SmoothDamp(_focus.y, GroundAt(focus), ref _groundVelocity, groundSmoothTime, float.PositiveInfinity,
                Time.unscaledDeltaTime);
    }
}
