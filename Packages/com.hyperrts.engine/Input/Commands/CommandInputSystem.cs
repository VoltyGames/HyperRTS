using HyperRTS.Presentation.Interaction;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HyperRTS.Input.Commands
{
    /// <summary>Turns hotkeys and world clicks into <see cref="PlayerCommand"/>s for the local player's selection.</summary>
    // OrderFirst (after selection) so every command consumer in the order phase sees this frame's commands.
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(SelectionSystem))]
    public partial class CommandInputSystem : InputMapSystem
    {
        private MatchInputActions _actions;
        private WorldPointer _pointer;
        private EntityQuery _selected;
        private FacingDragState _drag;

        protected override void OnCreate()
        {
            _actions = InputActionsProvider.Actions;
            _pointer = new WorldPointer(ref CheckedStateRef);
            _selected = SystemAPI.QueryBuilder().WithAll<Selected>().Build();
            RequireForUpdate<PendingCommand>();
            RequireForUpdate<PlacementState>();
            RequireForUpdate<PointerState>();
            RequireForUpdate<LocalPlayer>();
        }

        protected override InputActionMap Map => _actions.Commands;

        protected override void OnUpdate()
        {
            var player = SystemAPI.GetSingletonEntity<LocalPlayer>();
            var pending = SystemAPI.GetSingleton<PendingCommand>();

            if (SystemAPI.GetSingleton<PlacementState>().Active)
            {
                // Placement owns the mouse while active.
                pending.Type = CommandType.None;
                _drag = default;
            }
            else
            {
                HandleHotkeys(player, ref pending);
                if (!SystemAPI.GetSingleton<PointerState>().OverUI)
                {
                    HandleClicks(player, ref pending);
                }
            }

            UpdateDrag(player, ref pending);
            SystemAPI.SetSingleton(pending);
            SystemAPI.SetSingleton(_drag);
        }

        private void HandleHotkeys(Entity player, ref PendingCommand pending)
        {
            var commands = _actions.Commands;
            if (commands.AttackMove.WasPressedThisFrame())
            {
                pending = new PendingCommand { Type = CommandType.AttackMove };
            }

            if (commands.Patrol.WasPressedThisFrame())
            {
                pending = new PendingCommand { Type = CommandType.Patrol };
            }

            if (commands.Escort.WasPressedThisFrame())
            {
                pending = new PendingCommand { Type = CommandType.Escort };
            }

            if (commands.ReverseMove.WasPressedThisFrame())
            {
                pending = new PendingCommand { Type = CommandType.ReverseMove };
            }

            if (commands.Stop.WasPressedThisFrame())
            {
                pending.Type = CommandType.None;
                Issue(player, new PlayerCommand { Type = CommandType.Stop });
            }

            if (commands.HoldPosition.WasPressedThisFrame())
            {
                Issue(player, new PlayerCommand { Type = CommandType.SetStance, Argument = (int)Stance.HoldPosition });
            }
        }

        private void HandleClicks(Entity player, ref PendingCommand pending)
        {
            var commands = _actions.Commands;
            var rightClick = commands.Command.WasPressedThisFrame();
            var targetClick = commands.Confirm.WasPressedThisFrame() && pending.Type != CommandType.None;
            if (rightClick && pending.Type != CommandType.None)
            {
                pending.Type = CommandType.None;
                return;
            }

            if ((!rightClick && !targetClick) || !TryPick(out var target, out var ground))
            {
                return;
            }

            var queue = commands.Queue.IsPressed();
            var type = rightClick ? CommandType.Smart : pending.Type;
            var argument = rightClick ? 0 : pending.Argument;
            if (target == Entity.Null && FacingDragState.CanStart(type))
            {
                _drag = FacingDragState.Create(type, argument, queue, PointerScreen(), ground);
                return;
            }

            Issue(player, new PlayerCommand
            {
                Type = type, Target = target, Position = ground, Queue = queue, Argument = argument,
            });

            // Shift keeps the targeting mode for the next waypoint.
            if (targetClick && !queue)
            {
                pending.Type = CommandType.None;
            }
        }

        /// <summary>Follows the held drag, and on release issues its move at the press point.</summary>
        private void UpdateDrag(Entity player, ref PendingCommand pending)
        {
            if (!_drag.Held)
            {
                return;
            }

            if (IsInterrupted(pending))
            {
                _drag = default;
                return;
            }

            var screen = PointerScreen();
            if (_pointer.TryGround(screen, out var ground))
            {
                _drag.Pointer = ground;
            }

            _drag.Aim(screen);
            var button = _drag.Type == CommandType.Smart ? _actions.Commands.Command : _actions.Commands.Confirm;
            if (button.IsPressed())
            {
                return;
            }

            Issue(player, _drag.Command());
            if (_drag.Type != CommandType.Smart && !_drag.Queue)
            {
                pending.Type = CommandType.None;
            }

            _drag = default;
        }

        private bool IsInterrupted(PendingCommand pending)
        {
            if (InputActionsProvider.CancelPressed || !Application.isFocused)
            {
                return true;
            }

            if (_selected.IsEmpty)
            {
                return true;
            }

            // A pending move's drag ends with its mode: cancelled, or swapped for another by a hotkey.
            return _drag.Type != CommandType.Smart && pending.Type != _drag.Type;
        }

        private float2 PointerScreen() => _actions.Commands.Point.ReadValue<Vector2>();

        private bool TryPick(out Entity target, out float3 ground) =>
            _pointer.TryPick(ref CheckedStateRef, PointerScreen(), out target, out ground);

        private void Issue(Entity player, PlayerCommand command) =>
            EntityManager.GetBuffer<PlayerCommand>(player).Add(command);
    }
}
