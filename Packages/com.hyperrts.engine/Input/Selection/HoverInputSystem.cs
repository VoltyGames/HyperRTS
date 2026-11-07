using HyperRTS.Presentation.Interaction;
using HyperRTS.Input.Commands;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Input.Selection
{
    /// <summary>Writes the commandable entity under the pointer to <see cref="HoveredEntity"/> for the hover marker.</summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(CommandInputSystem))]
    public partial class HoverInputSystem : SystemBase
    {
        private MatchInputActions _actions;
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
            var ground = default(float3);
            var hasGround = false;
            if (!IsPointerBusy())
            {
                var point = _actions.Selection.Point.ReadValue<Vector2>();
                hasGround = _pointer.TryPick(ref CheckedStateRef, point, out hovered, out ground);
            }

            SystemAPI.SetSingleton(new HoveredEntity { Value = hovered, Ground = ground, HasGround = hasGround });
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
