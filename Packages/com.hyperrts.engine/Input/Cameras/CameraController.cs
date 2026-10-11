using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Interaction;
using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Input.Cameras
{
    /// <summary>RTS camera that orbits a ground focus point: pan, edge scroll, zoom by height, yaw and map clamping.</summary>
    [AddComponentMenu(HyperRTSMenu.Cameras + "Camera Controller")]
    [Icon(HyperRTSIcons.Cameras)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        // Mouse delta is per frame, so yaw scales with pixels dragged rather than time.
        private const float DegreesPerPixel = 1f / 500f;

        [Header("Movement")]
        [Tooltip("Pan speed (arrow keys, screen edges) in units per second at minimum zoom; scales with height.")]
        public float moveSpeed = 10f;

        [Tooltip("Player preference scaling the pan speed; set at runtime by a settings menu.")]
        [Min(0f)]
        public float speedMultiplier = 1f;

        [Tooltip("Yaw in degrees per 500 pixels of middle-mouse drag.")]
        public float rotationSpeed = 100f;

        [Header("Edge Scrolling")]
        [Tooltip("Pan when the cursor touches a screen edge.")]
        public bool edgeScrolling = true;

        [Tooltip("Width in pixels of the screen border that triggers edge scrolling.")]
        public float edgeSize = 8f;

        [Header("Zoom")]
        [Tooltip("Height change per mouse-scroll notch.")]
        public float zoomStep = 4f;

        [Tooltip("Closest zoom (minimum camera height).")]
        public float minZoom = 10f;

        [Tooltip("Farthest zoom (maximum camera height).")]
        public float maxZoom = 50f;

        [Header("Default View")]
        [Tooltip("Position the camera resets to (Home key).")]
        public Vector3 defaultPosition = new(0, 35, -50);

        [Tooltip("Rotation the camera resets to (Home key).")]
        public Quaternion defaultRotation = Quaternion.Euler(30, 0, 0);

        [Tooltip("Open the match centred on the local player's buildings, if they have any once they join.")]
        public bool startAtHome = true;

        [Tooltip("Seconds after the local player joins to keep looking for their buildings (network ghosts can arrive a few snapshots apart).")]
        [Min(0f)]
        public float homeSearchSeconds = 3f;

        private readonly HomeBase _home = new();
        private bool _homeDone;
        private float _homeDeadline = float.NaN;

        private readonly LiveQuery _map = new(entityManager =>
            entityManager.CreateEntityQuery(ComponentType.ReadOnly<MapSettings>()));

        private readonly LiveQuery _focusRequest = new(entityManager =>
            entityManager.CreateEntityQuery(ComponentType.ReadWrite<CameraFocusRequest>()));

        private RTSInputActions _actions;

        /// <summary>Centres the view on a world point, keeping height and rotation.</summary>
        public void FocusOn(Vector3 worldPoint) => PlaceAt(ClampToMap(worldPoint), transform.position.y);

        private void Awake()
        {
            _actions = InputActionsProvider.Actions;
        }

        private void OnEnable() => InputActionsProvider.Enable(_actions.Camera);

        private void OnDisable() => InputActionsProvider.Disable(_actions.Camera);

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
            transform.SetPositionAndRotation(defaultPosition, defaultRotation);
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

            var focus = Focus();
            var height = transform.position.y;
            var speed = moveSpeed * speedMultiplier * Mathf.Max(1f, height / Mathf.Max(minZoom, 0.01f));
            focus += PanDirection() * (speed * Time.unscaledDeltaTime);

            var scroll = camera.Zoom.ReadValue<float>();
            if (scroll != 0f)
            {
                height = Mathf.Clamp(height - Mathf.Sign(scroll) * zoomStep, minZoom, maxZoom);
            }

            if (camera.Rotate.IsPressed())
            {
                var yaw = camera.Look.ReadValue<Vector2>().x * rotationSpeed * DegreesPerPixel;
                transform.Rotate(0f, yaw, 0f, Space.World);
            }

            PlaceAt(ClampToMap(focus), height);
        }

        /// <summary>World-space XZ pan from the arrow keys plus edge scrolling, relative to the camera's yaw.</summary>
        private Vector3 PanDirection()
        {
            var input = _actions.Camera.Pan.ReadValue<Vector2>() + EdgeDirection();
            var forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            return Vector3.ClampMagnitude(right * input.x + forward * input.y, 1f);
        }

        private Vector2 EdgeDirection()
        {
            if (!edgeScrolling || !Application.isFocused)
            {
                return Vector2.zero;
            }

            var pointer = _actions.Camera.Point.ReadValue<Vector2>();
            if (pointer.x < 0f || pointer.y < 0f || pointer.x > Screen.width || pointer.y > Screen.height)
            {
                return Vector2.zero;
            }

            var x = pointer.x <= edgeSize ? -1f : pointer.x >= Screen.width - edgeSize ? 1f : 0f;
            var y = pointer.y <= edgeSize ? -1f : pointer.y >= Screen.height - edgeSize ? 1f : 0f;
            return new Vector2(x, y);
        }

        /// <summary>The y = 0 ground point the camera looks at; zoom and panning keep the camera relative to it.</summary>
        private Vector3 Focus() =>
            ViewGround.Focus(default, transform.position, transform.forward, float.PositiveInfinity);

        /// <summary>Moves the camera so it looks at <paramref name="focus"/> from <paramref name="height"/>.</summary>
        private void PlaceAt(Vector3 focus, float height)
        {
            focus.y = 0f;
            var forward = transform.forward;

            // The inverse of Focus: a view ray that never meets the ground looks at the point below the camera.
            transform.position = forward.y > -1e-5f
                ? new Vector3(focus.x, height, focus.z)
                : focus - forward * (height / -forward.y);
        }

        private Vector3 ClampToMap(Vector3 point)
        {
            if (!DefaultWorld.TryGetEntityManager(out var entityManager))
            {
                return point;
            }

            return _map.In(entityManager).TryGetSingleton(out MapSettings map) ? (Vector3)map.Clamp(point) : point;
        }

        /// <summary>
        /// The local player's base, looked for during <see cref="homeSearchSeconds"/> after the local player
        /// exists (while <see cref="startAtHome"/> is set); a player with no buildings by then keeps the view.
        /// </summary>
        private bool TryFindHomeOnce(out Vector3 home)
        {
            home = default;
            if (_homeDone || !startAtHome || !DefaultWorld.TryGetEntityManager(out var entityManager))
            {
                return false;
            }

            if (!_home.TryGetLocalFaction(entityManager, out var faction))
            {
                return false;
            }

            if (float.IsNaN(_homeDeadline))
            {
                _homeDeadline = Time.unscaledTime + homeSearchSeconds;
            }

            var found = _home.TryFind(entityManager, faction, out home);
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

            var query = _focusRequest.In(entityManager);
            if (!query.TryGetSingleton(out CameraFocusRequest request) || !request.Pending)
            {
                return false;
            }

            query.SetSingleton(new CameraFocusRequest());
            point = request.Point;
            return true;
        }
    }
}
