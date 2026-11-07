using HyperRTS.Input.Placement;
using HyperRTS.Presentation.Interaction;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine.InputSystem;

namespace HyperRTS.Input.Commands
{
    /// <summary>Escape undoes the topmost client mode (<see cref="InteractionState.Cancel"/>) before the other input systems read them.</summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(CommandInputSystem))]
    [UpdateBefore(typeof(HotkeyInputSystem))]
    [UpdateBefore(typeof(PlacementInputSystem))]
    public partial class CancelInputSystem : InputMapSystem
    {
        private RTSInputActions _actions;

        protected override void OnCreate()
        {
            _actions = InputActionsProvider.Actions;
            RequireForUpdate<PlacementState>();
            RequireForUpdate<PendingCommand>();
            RequireForUpdate<CommandCardPage>();
        }

        protected override InputActionMap Map => _actions.Commands;

        protected override void OnUpdate()
        {
            if (!InputActionsProvider.CancelPressed)
            {
                return;
            }

            var state = new InteractionState
            {
                Placement = SystemAPI.GetSingleton<PlacementState>(),
                Pending = SystemAPI.GetSingleton<PendingCommand>(),
                Page = SystemAPI.GetSingleton<CommandCardPage>(),
            };
            if (state.Cancel())
            {
                SystemAPI.SetSingleton(state.Placement);
                SystemAPI.SetSingleton(state.Pending);
                SystemAPI.SetSingleton(state.Page);
            }
        }
    }
}
