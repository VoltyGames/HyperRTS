using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Interaction;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HyperRTS.Input.Selection
{
    /// <summary>Turns mouse and control-group keys into the <see cref="SelectionInput"/> singleton.</summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(SelectionSystem))]
    public partial class SelectionInputSystem : SystemBase
    {
        private const float DragThresholdPixels = 6f;
        private const float DoubleClickSeconds = 0.3f;
        private const float DoubleClickPixels = 16f;

        private RTSInputActions _actions;
        private InputAction[] _groupKeys;
        private Camera _camera;

        // Published each frame for the drag-box UI.
        private SelectionDragState _drag;
        private bool _pressed;

        private float2 _lastClickPosition;
        private double _lastClickTime;

        protected override void OnCreate()
        {
            _actions = new RTSInputActions();

            var selection = _actions.Selection;
            _groupKeys = new[] { selection.Group1, selection.Group2, selection.Group3, selection.Group4, selection.Group5 };

            SingletonUtility.Ensure<SelectionInput>(EntityManager);
            SingletonUtility.Ensure<SelectionDragState>(EntityManager);
            SingletonUtility.Ensure<PendingCommand>(EntityManager);
            SingletonUtility.Ensure<PlacementState>(EntityManager);
            SingletonUtility.Ensure<PointerState>(EntityManager);
        }

        // Not in OnCreate: without domain reload the Input System wipes action states after the world is created.
        protected override void OnStartRunning() => _actions.Selection.Enable();

        protected override void OnStopRunning() => _actions.Selection.Disable();

        protected override void OnDestroy()
        {
            _actions?.Dispose();
            _actions = null;
        }

        protected override void OnUpdate()
        {
            var input = new SelectionInput { Command = SelectionCommand.None };

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_camera != null)
            {
                UpdateGesture(_camera, ref input);
            }

            if (input.Command == SelectionCommand.None)
            {
                ReadControlGroups(ref input);
            }

            SystemAPI.SetSingleton(input);
            SystemAPI.SetSingleton(_drag);
        }

        /// <summary>Clicks over the HUD, in placement mode or confirming a targeted command are not selections.</summary>
        private bool IsPointerClaimed() =>
            SystemAPI.GetSingleton<PointerState>().OverUI ||
            SystemAPI.GetSingleton<PlacementState>().Active ||
            SystemAPI.GetSingleton<PendingCommand>().Type != CommandType.None;

        private void UpdateGesture(Camera camera, ref SelectionInput input)
        {
            var select = _actions.Selection.Select;
            var pointer = (float2)_actions.Selection.Point.ReadValue<Vector2>();

            if (select.WasPressedThisFrame() && !IsPointerClaimed())
            {
                _pressed = true;
                _drag = new SelectionDragState { StartScreen = pointer, CurrentScreen = pointer };
            }

            if (_pressed && select.IsPressed())
            {
                _drag.CurrentScreen = pointer;
                _drag.IsDragging |= math.distance(pointer, _drag.StartScreen) > DragThresholdPixels;
            }

            if (!(select.WasReleasedThisFrame() && _pressed))
            {
                return;
            }

            _pressed = false;
            WriteRelease(camera, pointer, ref input);
            _drag.IsDragging = false;
        }

        private void WriteRelease(Camera camera, float2 pointer, ref SelectionInput input)
        {
            // Only a release issues a command, so modifiers and view-projection are read here.
            input.Additive = _actions.Selection.Additive.IsPressed();
            input.Subtract = _actions.Selection.Subtract.IsPressed();
            input.ScreenSize = new float2(Screen.width, Screen.height);

            var vp = camera.projectionMatrix * camera.worldToCameraMatrix;
            input.ViewProjection = new float4x4(vp.GetColumn(0), vp.GetColumn(1), vp.GetColumn(2), vp.GetColumn(3));

            if (_drag.IsDragging)
            {
                input.Command = SelectionCommand.DragRelease;
                input.DragMin = math.min(_drag.StartScreen, pointer);
                input.DragMax = math.max(_drag.StartScreen, pointer);
                return;
            }

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

        /// <summary>Ctrl+N assigns the selection to group N, N recalls it, Shift+N adds it.</summary>
        private void ReadControlGroups(ref SelectionInput input)
        {
            for (var i = 0; i < _groupKeys.Length; i++)
            {
                if (!_groupKeys[i].WasPressedThisFrame())
                {
                    continue;
                }

                var assign = _actions.Selection.AssignGroup.IsPressed();
                input.Command = assign ? SelectionCommand.AssignGroup : SelectionCommand.RecallGroup;
                input.Additive = !assign && _actions.Selection.Additive.IsPressed();
                input.Group = (byte)i;
                return;
            }
        }
    }
}
