using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Interaction;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Navigation.Heights;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Input.Cameras
{
    /// <summary>RTS camera that orbits a ground focus point: pan, edge scroll, zoom by height, yaw and map clamping.</summary>
    [AddComponentMenu(HyperRTSMenu.Scene + "Rig/Camera Controller")]
    [Icon(HyperRTSIcons.CameraController)]
    [HelpURL(HyperRTSDocs.Input)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public partial class CameraController : PresentationBehaviour
    {
        // Mouse delta is per frame, so yaw scales with pixels dragged rather than time.
        private const float DegreesPerPixel = 1f / 500f;

        [Header("Movement")]
        [Tooltip("Pan speed (arrow keys, screen edges) in units per second for every 10 units of camera height.")]
        public float moveSpeed = 10f;

        [Tooltip("Player preference scaling the pan speed; set at runtime by a settings menu.")]
        [Min(0f)]
        public float speedMultiplier = 1f;

        [Tooltip("Yaw in degrees per 500 pixels of middle-mouse drag.")]
        public float rotationSpeed = 100f;

        [Tooltip("Yaw in degrees per second while a Turn key is held.")]
        public float turnSpeed = 90f;

        [Header("Edge Scrolling")]
        [Tooltip("Pan when the cursor touches a screen edge.")]
        public bool edgeScrolling = true;

        [Tooltip("Width in pixels of the screen border that triggers edge scrolling.")]
        public float edgeSize = 8f;

        [Header("Default View")]
        [Tooltip("Position the camera resets to (Home key).")]
        public Vector3 defaultPosition = new(0, 35, -50);

        [Tooltip("Rotation the camera resets to (Home key); only its yaw is used, pitch follows the zoom.")]
        public Quaternion defaultRotation = Quaternion.Euler(30, 0, 0);

        [Tooltip("Open the match centred on the local player's buildings, if they have any once they join.")]
        public bool startAtHome = true;

        [Tooltip("Seconds after the local player joins to keep looking for their buildings (network ghosts can arrive a few snapshots apart).")]
        [Min(0f)]
        public float homeSearchSeconds = 3f;

        private readonly HomeBase _home = new();
        private bool _homeDone;
        private float _homeDeadline = float.NaN;

        private readonly LiveSingleton<MapSettings> _map = new();
        private readonly LiveSingleton<CameraFocusRequest> _focusRequest = new();
        private readonly LiveSingleton<ScrollEdge> _scrollEdge = new();

        private MatchInputActions _actions;
        private float _yaw;
        private float _height;
        private float _targetHeight;
        private float _zoomVelocity;
        private Vector3 _focus;
        private float _groundVelocity;
        private Camera _camera;
        private float _farClip;

        /// <summary>Centres the view on a world point, keeping height and rotation.</summary>
        public void FocusOn(Vector3 worldPoint)
        {
            var focus = ClampToMap(worldPoint);
            focus.y = GroundAt(focus);
            _groundVelocity = 0f;
            PlaceAt(focus, _height);
        }

        protected override void OnAwake()
        {
            _actions = InputActionsProvider.Actions;
            _camera = GetComponent<Camera>();
            _farClip = _camera.farClipPlane;
        }

        private void OnEnable() => InputActionsProvider.Enable(_actions.Camera);

        protected override void OnDisabled()
        {
            InputActionsProvider.Disable(_actions.Camera);
            StopTreeFade();
        }

        private void Start()
        {
            ResetViewToDefault();
        }

        [ContextMenu("Capture Current Transform As Default")]
        private void CaptureCurrentTransformAsDefault()
        {
            defaultPosition = transform.position;
            defaultRotation = transform.rotation;
        }

        [ContextMenu("Reset View To Default")]
        private void ResetViewToDefault()
        {
            var focus = ViewGround.Focus(MatchTerrain(), defaultPosition, defaultRotation * Vector3.forward,
                float.PositiveInfinity);
            _yaw = defaultRotation.eulerAngles.y;
            _targetHeight = Mathf.Clamp(defaultPosition.y - focus.y, minZoom, maxZoom);
            _zoomVelocity = 0f;
            _groundVelocity = 0f;
            PlaceAt(focus, _targetHeight);
        }

        private void Update()
        {
            var camera = _actions.Camera;
            if (camera.Reset.WasPressedThisFrame())
            {
                ResetViewToDefault();
                return;
            }

            if (TryTakeFocusRequest(out var requested))
            {
                FocusOn(requested);
                return;
            }

            if (TryFindHomeOnce(out var home))
            {
                FocusOn(home);
                return;
            }

            // A disabled Point reads (0, 0), the bottom-left corner, and would edge scroll behind a menu.
            if (!camera.enabled)
            {
                EdgeDirection(false);
                return;
            }

            var speed = moveSpeed * speedMultiplier * _height / 10f;
            var focus = ClampToMap(_focus + PanDirection() * (speed * Time.unscaledDeltaTime));

            var scroll = camera.Zoom.ReadValue<float>();
            if (scroll != 0f)
            {
                _targetHeight = ZoomedHeight(_targetHeight, scroll, zoomFraction, minZoom, maxZoom);
            }

            if (camera.Rotate.IsPressed())
            {
                _yaw += camera.Look.ReadValue<Vector2>().x * rotationSpeed * DegreesPerPixel;
            }

            _yaw += camera.Turn.ReadValue<float>() * turnSpeed * Time.unscaledDeltaTime;

            focus.y = SmoothedGround(focus);
            var height = Mathf.SmoothDamp(_height, _targetHeight, ref _zoomVelocity, zoomSmoothTime,
                float.PositiveInfinity, Time.unscaledDeltaTime);
            PlaceAt(focus, height);
        }

        /// <summary>World-space XZ pan from the arrow keys plus edge scrolling, relative to the camera's yaw.</summary>
        private Vector3 PanDirection()
        {
            var input = _actions.Camera.Pan.ReadValue<Vector2>() + EdgeDirection(true);
            var forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            return Vector3.ClampMagnitude(right * input.x + forward * input.y, 1f);
        }

        /// <summary>The edge the pointer scrolls against, published for the cursor; input may pause it.</summary>
        private Vector2 EdgeDirection(bool pointerLive)
        {
            var hasWorld = DefaultWorld.TryGetEntityManager(out var entityManager);
            var state = default(ScrollEdge);
            var shared = hasWorld && _scrollEdge.TryGet(entityManager, out state);
            state.Direction = int2.zero;
            var scrolls = pointerLive && edgeScrolling && Application.isFocused;
            if (scrolls && !state.Paused)
            {
                var screen = new float2(Screen.width, Screen.height);
                state.Direction = ScrollEdge.At(_actions.Camera.Point.ReadValue<Vector2>(), screen, edgeSize);
            }

            if (shared)
            {
                _scrollEdge.Set(entityManager, state);
            }

            return new Vector2(state.Direction.x, state.Direction.y);
        }

        /// <summary>
        /// Looks at <paramref name="focus"/> (its y is the ground there) from <paramref name="height"/> above it,
        /// pitched for that height. The focus stays put, so zooming never slides the view.
        /// </summary>
        private void PlaceAt(Vector3 focus, float height)
        {
            _focus = focus;
            _height = height;
            var pitch = PitchAt(height);
            _camera.farClipPlane = Mathf.Lerp(_farClip, Mathf.Max(farClipAtTilt, _farClip), TiltAt(height));
            transform.rotation = Quaternion.Euler(pitch, _yaw, 0f);
            var forward = transform.forward;

            // A view ray that never meets the ground looks at the point below the camera.
            var position = forward.y > -1e-5f
                ? new Vector3(focus.x, focus.y + height, focus.z)
                : focus - forward * (height / -forward.y);
            var terrainY = GroundAt(position);
            position.y = ClearedHeight(position.y, terrainY, terrainClearance);
            transform.position = position;
            ShareTreeFade();
        }

        private Vector3 ClampToMap(Vector3 point)
        {
            if (!DefaultWorld.TryGetEntityManager(out var entityManager))
            {
                return point;
            }

            return _map.TryGet(entityManager, out var map) ? (Vector3)map.Clamp(point) : point;
        }

        /// <summary>
        /// The local player's base, searched for over <see cref="homeSearchSeconds"/> when <see cref="startAtHome"/>
        /// is set. A player with no buildings by then keeps the view.
        /// </summary>
        private bool TryFindHomeOnce(out Vector3 home)
        {
            home = default;
            if (_homeDone || !startAtHome || !DefaultWorld.TryGetEntityManager(out var entityManager))
            {
                return false;
            }

            if (!_home.TryGetLocalOwner(entityManager, out var owner))
            {
                return false;
            }

            if (float.IsNaN(_homeDeadline))
            {
                _homeDeadline = Time.unscaledTime + homeSearchSeconds;
            }

            var found = _home.TryFind(entityManager, owner, out home);
            var expired = Time.unscaledTime >= _homeDeadline;
            _homeDone = found || expired;
            return found;
        }

        /// <summary>Takes the point the HUD (minimap) asked to centre on, clearing the request.</summary>
        private bool TryTakeFocusRequest(out Vector3 point)
        {
            point = default;
            if (!DefaultWorld.TryGetEntityManager(out var entityManager))
            {
                return false;
            }

            if (!_focusRequest.TryGet(entityManager, out var request) || !request.Pending)
            {
                return false;
            }

            _focusRequest.Set(entityManager, new CameraFocusRequest());
            point = request.Point;
            return true;
        }
    }
}
