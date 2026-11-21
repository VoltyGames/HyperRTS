using UnityEngine;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>
    /// How much world one pixel of a 1080p screen covers, read from the camera once per frame. Overlay sizes are
    /// authored in these pixels so they keep their share of the screen at any resolution.
    /// </summary>
    public readonly struct ReferencePixels
    {
        public const float ScreenHeight = 1080f;

        private readonly Vector3 _cameraPosition;
        private readonly Vector3 _cameraForward;
        private readonly bool _orthographic;
        private readonly float _orthographicPixel;
        private readonly float _pixelPerDepth;
        private readonly Matrix4x4 _viewProjection;
        private readonly float _aspect;

        public ReferencePixels(Camera camera)
        {
            var transform = camera.transform;
            _cameraPosition = transform.position;
            _cameraForward = transform.forward;
            _orthographic = camera.orthographic;
            _orthographicPixel = camera.orthographicSize * 2f / ScreenHeight;
            _pixelPerDepth = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 2f / ScreenHeight;
            _viewProjection = camera.projectionMatrix * camera.worldToCameraMatrix;
            _aspect = camera.aspect;
        }

        /// <summary>World units one reference pixel covers at <paramref name="point"/>.</summary>
        public float WorldSize(Vector3 point)
        {
            if (_orthographic)
            {
                return _orthographicPixel;
            }

            return Vector3.Dot(point - _cameraPosition, _cameraForward) * _pixelPerDepth;
        }

        /// <summary>
        /// Reference pixels between two points as the camera draws them, so a steep view shortens a standing unit.
        /// A point behind the camera spans the whole screen.
        /// </summary>
        public float Projected(Vector3 from, Vector3 to)
        {
            var a = _viewProjection * new Vector4(from.x, from.y, from.z, 1f);
            var b = _viewProjection * new Vector4(to.x, to.y, to.z, 1f);
            if (a.w <= 0f || b.w <= 0f)
            {
                return ScreenHeight;
            }

            var ndc = new Vector2(a.x / a.w - b.x / b.w, a.y / a.w - b.y / b.w);
            return new Vector2(ndc.x * _aspect, ndc.y).magnitude * ScreenHeight * 0.5f;
        }
    }
}
