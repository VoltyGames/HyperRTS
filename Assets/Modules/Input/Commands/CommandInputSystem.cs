using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Interaction;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Input.Commands
{
    /// <summary>Turns hotkeys and world clicks into <see cref="PlayerCommand"/>s for the local player's selection.</summary>
    // OrderFirst (after selection) so every command consumer in the order phase sees this frame's commands.
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(SelectionSystem))]
    public partial class CommandInputSystem : SystemBase
    {
        private RTSInputActions _actions;
        private WorldPointer _pointer;

        protected override void OnCreate()
        {
            _actions = new RTSInputActions();
            _pointer = new WorldPointer(ref CheckedStateRef);

            SingletonUtility.Ensure<PendingCommand>(EntityManager);
            SingletonUtility.Ensure<PlacementState>(EntityManager);
            SingletonUtility.Ensure<PointerState>(EntityManager);
            RequireForUpdate<LocalPlayer>();
        }

        // Not in OnCreate: without domain reload the Input System wipes action states after the world is created.
        protected override void OnStartRunning() => _actions.Commands.Enable();

        protected override void OnStopRunning() => _actions.Commands.Disable();

        protected override void OnDestroy()
        {
            _actions?.Dispose();
            _actions = null;
        }

        protected override void OnUpdate()
        {
            var player = SystemAPI.GetSingletonEntity<LocalPlayer>();
            var pending = SystemAPI.GetSingleton<PendingCommand>();

            if (_actions.Commands.Cancel.WasPressedThisFrame() || SystemAPI.GetSingleton<PlacementState>().Active)
            {
                // Placement owns the mouse while active.
                pending.Type = CommandType.None;
            }
            else
            {
                HandleHotkeys(player, ref pending);
                if (!SystemAPI.GetSingleton<PointerState>().OverUI)
                {
                    HandleClicks(player, ref pending);
                }
            }

            SystemAPI.SetSingleton(pending);
        }

        private void HandleHotkeys(Entity player, ref PendingCommand pending)
        {
            var commands = _actions.Commands;
            if (commands.AttackMove.WasPressedThisFrame())
            {
                pending.Type = CommandType.AttackMove;
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
            Issue(player, new PlayerCommand { Type = type, Target = target, Position = ground, Queue = queue });

            // Shift keeps the targeting mode for the next waypoint.
            if (targetClick && !queue)
            {
                pending.Type = CommandType.None;
            }
        }

        private bool TryPick(out Entity target, out float3 ground) => _pointer.TryPick(ref CheckedStateRef,
            _actions.Commands.Point.ReadValue<Vector2>(), out target, out ground);

        private void Issue(Entity player, PlayerCommand command) =>
            EntityManager.GetBuffer<PlayerCommand>(player).Add(command);
    }
}
