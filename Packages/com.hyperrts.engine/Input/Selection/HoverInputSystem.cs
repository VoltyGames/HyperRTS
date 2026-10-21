using HyperRTS.Client.Interaction;
using HyperRTS.Core;
using HyperRTS.Input.Commands;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Input.Selection
{
    /// <summary>Writes the commandable entity under the pointer to <see cref="HoveredEntity"/> for the hover marker.</summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(CommandInputSystem))]
    public partial class HoverInputSystem : SystemBase
    {
        private RTSInputActions _actions;
        private WorldPointer _pointer;

        protected override void OnCreate()
        {
            _actions = InputActionsProvider.Actions;
            _pointer = new WorldPointer(ref CheckedStateRef);
            RequireForUpdate<HoveredEntity>();
            RequireForUpdate<PointerState>();
            RequireForUpdate<PlacementState>();
            RequireForUpdate<SelectionDragState>();
        }

        protected override void OnUpdate()
        {
            var hovered = Entity.Null;
            if (!IsPointerBusy())
            {
                var point = _actions.Selection.Point.ReadValue<Vector2>();
                _pointer.TryPick(ref CheckedStateRef, point, out hovered, out _);
            }

            SystemAPI.SetSingleton(new HoveredEntity { Value = hovered });
        }

        private bool IsPointerBusy()
        {
            if (SystemAPI.GetSingleton<PointerState>().OverUI)
            {
                return true;
            }

            if (SystemAPI.GetSingleton<PlacementState>().Active)
            {
                return true;
            }

            return SystemAPI.GetSingleton<SelectionDragState>().IsDragging;
        }
    }
}
