using HyperRTS.Core.Selection.Input;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Core.Selection
{
    /// <summary>
    /// Reads the camera and Input System (the managed side), classifies the gesture into a
    /// click/drag-box/double-click, and writes the <see cref="SelectionInput"/> singleton.
    /// </summary>
    [UpdateInGroup(typeof(OrderSystemGroup))]
    [UpdateBefore(typeof(SelectionSystem))]
    public partial class SelectionInputSystem : SystemBase
    {
        private const float DragThresholdPixels = 6f;
        private const float DoubleClickSeconds = 0.3f;
        private const float DoubleClickPixels = 16f;

        // Live drag state (screen pixels, bottom-left), read by SelectionDragBoxUI.
        public static bool IsDragging;
        public static Vector2 DragStartScreen;
        public static Vector2 DragCurrentScreen;

        private RTSInputActions _actions;
        private Camera _camera;

        private float2 _pressPosition;
        private bool _pressed;

        private float2 _lastClickPosition;
        private double _lastClickTime;

        protected override void OnCreate()
        {
            _actions = new RTSInputActions();
            _actions.Selection.Enable();

            var singleton = EntityManager.CreateEntity(typeof(SelectionInput));
            EntityManager.SetComponentData(singleton, new SelectionInput { Command = SelectionCommand.None });
        }

        protected override void OnDestroy()
        {
            if (_actions != null)
            {
                _actions.Selection.Disable();
                _actions.Dispose();
                _actions = null;
            }

            IsDragging = false;
        }

        protected override void OnUpdate()
        {
            var input = new SelectionInput { Command = SelectionCommand.None };

            var camera = ResolveCamera();
            if (camera != null)
            {
                UpdateGesture(camera, ref input);
            }

            SystemAPI.SetSingleton(input);
        }

        private Camera ResolveCamera()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            return _camera;
        }

        private void UpdateGesture(Camera camera, ref SelectionInput input)
        {
            var select = _actions.Selection.Select;
            var pointer = (float2)_actions.Selection.Point.ReadValue<Vector2>();

            if (select.WasPressedThisFrame())
            {
                _pressed = true;
                _pressPosition = pointer;
                IsDragging = false;
                DragStartScreen = pointer;
                DragCurrentScreen = pointer;
            }

            if (_pressed && select.IsPressed())
            {
                DragCurrentScreen = pointer;
                if (math.distance(pointer, _pressPosition) > DragThresholdPixels)
                {
                    IsDragging = true;
                }
            }

            if (!(select.WasReleasedThisFrame() && _pressed))
            {
                return;
            }

            _pressed = false;

            // Only a release produces a command, so the modifiers and view-projection are computed
            // here rather than every frame (SelectionSystem ignores them while Command == None).
            input.Additive = _actions.Selection.Additive.IsPressed();
            input.Subtract = _actions.Selection.Subtract.IsPressed();
            input.ScreenSize = new float2(Screen.width, Screen.height);

            var vp = camera.projectionMatrix * camera.worldToCameraMatrix;
            input.ViewProjection = new float4x4(vp.GetColumn(0), vp.GetColumn(1), vp.GetColumn(2), vp.GetColumn(3));

            if (IsDragging)
            {
                input.Command = SelectionCommand.DragRelease;
                input.DragMin = math.min(_pressPosition, pointer);
                input.DragMax = math.max(_pressPosition, pointer);
            }
            else
            {
                var now = SystemAPI.Time.ElapsedTime;
                var isDoubleClick = now - _lastClickTime <= DoubleClickSeconds &&
                                    math.distance(pointer, _lastClickPosition) <= DoubleClickPixels;
                input.Command = isDoubleClick ? SelectionCommand.DoubleClick : SelectionCommand.Click;

                var ray = camera.ScreenPointToRay(new Vector3(pointer.x, pointer.y, 0f));
                input.RayOrigin = ray.origin;
                input.RayDirection = math.normalizesafe((float3)ray.direction);
                input.RayDistance = camera.farClipPlane;

                _lastClickTime = now;
                _lastClickPosition = pointer;
            }

            IsDragging = false;
        }
    }
}
