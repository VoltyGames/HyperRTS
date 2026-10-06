using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Interaction;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Input.Commands
{
    /// <summary>Drives building placement mode: moves the snapped ghost and confirms it as a PlaceBuilding command.</summary>
    // After CommandInputSystem, so a cancelling right-click can't also become a smart command this frame.
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(CommandInputSystem))]
    public partial class PlacementInputSystem : SystemBase
    {
        private RTSInputActions _actions;
        private WorldPointer _pointer;

        protected override void OnCreate()
        {
            _actions = new RTSInputActions();
            _pointer = new WorldPointer(ref CheckedStateRef);

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
            var placement = SystemAPI.GetSingleton<PlacementState>();
            if (!placement.Active)
            {
                return;
            }

            var commands = _actions.Commands;
            if (placement.Prefab == Entity.Null || commands.Cancel.WasPressedThisFrame() ||
                commands.Command.WasPressedThisFrame())
            {
                SystemAPI.SetSingleton(new PlacementState());
                return;
            }

            if (SystemAPI.GetSingleton<PointerState>().OverUI || !TryGetGround(out var ground))
            {
                return;
            }

            MoveGhost(ref placement, ground);
            if (commands.Confirm.WasPressedThisFrame() && placement.Valid)
            {
                Place(ref placement, commands.Queue.IsPressed());
            }

            SystemAPI.SetSingleton(placement);
        }

        private void MoveGhost(ref PlacementState placement, float3 ground)
        {
            if (!SystemAPI.TryGetSingleton<MapSettings>(out var map))
            {
                placement.Position = ground;
                placement.Valid = true;
                return;
            }

            var footprint = SystemAPI.GetComponent<NavObstacle>(placement.Prefab).Size;
            SystemAPI.TryGetSingleton<NavGrid>(out var grid);
            CompleteDependency();
            placement.Position = PlacementMath.Snap(in map, ground, footprint);
            placement.Valid = PlacementMath.IsValid(in map, in grid, placement.Position, footprint);
        }

        private void Place(ref PlacementState placement, bool queue)
        {
            var player = SystemAPI.GetSingletonEntity<LocalPlayer>();
            EntityManager.GetBuffer<PlayerCommand>(player).Add(new PlayerCommand
            {
                Type = CommandType.PlaceBuilding,
                Prefab = placement.Prefab,
                Position = placement.Position,
                Queue = queue,
            });

            // Shift keeps placing copies of the same building.
            if (!queue)
            {
                placement = new PlacementState();
            }
        }

        private bool TryGetGround(out float3 ground) => _pointer.TryPick(ref CheckedStateRef,
            _actions.Commands.Point.ReadValue<Vector2>(), out _, out ground);
    }
}
