using HyperRTS.Client.Interaction;
using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Input.Commands
{
    /// <summary>
    /// Drives building placement mode: moves the snapped ghost and confirms it as a PlaceBuilding command, or for a line
    /// prefab drags the line from press to release and sends it as a PlaceLine command.
    /// </summary>
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
            _actions = InputActionsProvider.Actions;
            _pointer = new WorldPointer(ref CheckedStateRef);
            RequireForUpdate<PlacementState>();
            RequireForUpdate<PointerState>();
            RequireForUpdate<LocalPlayer>();
            RequireForUpdate<MapSettings>();
            RequireForUpdate<NavGrid>();
        }

        // Not in OnCreate: without domain reload the Input System wipes action states after the world is created.
        protected override void OnStartRunning() => InputActionsProvider.Enable(_actions.Commands);

        protected override void OnStopRunning() => InputActionsProvider.Disable(_actions.Commands);

        protected override void OnUpdate()
        {
            var placement = SystemAPI.GetSingleton<PlacementState>();
            if (!placement.Active)
            {
                return;
            }

            var commands = _actions.Commands;
            var cancelled = InputActionsProvider.CancelPressed || commands.Command.WasPressedThisFrame();
            if (placement.Prefab == Entity.Null || cancelled)
            {
                SystemAPI.SetSingleton(new PlacementState());
                return;
            }

            var aimed = TryAim(out var ground);
            if (SystemAPI.HasComponent<LinePlacement>(placement.Prefab))
            {
                // A drag released over the HUD still ends where the cursor last touched the ground.
                if (aimed)
                {
                    MoveLine(ref placement, ground);
                }

                DragLine(ref placement, aimed, commands.Queue.IsPressed());
                SystemAPI.SetSingleton(placement);
                return;
            }

            if (!aimed)
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
            var map = SystemAPI.GetSingleton<MapSettings>();
            var footprint = SystemAPI.GetComponent<NavObstacle>(placement.Prefab).Size;
            var surface = BuildingPlacement.GetSurface(EntityManager, placement.Prefab);
            var grid = SystemAPI.GetSingleton<NavGrid>();
            CompleteDependency();
            placement.Valid = PlacementMath.Resolve(in map, in grid, ground, footprint, surface, out placement.Position);
        }

        private void MoveLine(ref PlacementState placement, float3 ground)
        {
            var map = SystemAPI.GetSingleton<MapSettings>();
            var footprint = SystemAPI.GetComponent<NavObstacle>(placement.Prefab).Size;
            var surface = BuildingPlacement.GetSurface(EntityManager, placement.Prefab);
            var grid = SystemAPI.GetSingleton<NavGrid>();
            CompleteDependency();
            placement.Position = ground;
            var start = placement.Dragging ? placement.LineStart : ground;
            var line = new PlacementLine(map, start, ground, footprint);
            var segments = SystemAPI.GetSingletonBuffer<PlacementSegment>();
            segments.Clear();
            placement.ValidSegments = 0;
            for (var i = 0; i < line.Count; i++)
            {
                var valid = PlacementMath.Resolve(map, grid, line.Segment(i), line.Footprint, surface, out var center);
                segments.Add(new PlacementSegment { Center = center, Footprint = line.Footprint, Valid = valid });
                placement.ValidSegments += valid ? 1 : 0;
            }

            placement.Valid = placement.ValidSegments > 0;
        }

        private void DragLine(ref PlacementState placement, bool aimed, bool queue)
        {
            var confirm = _actions.Commands.Confirm;
            if (!placement.Dragging)
            {
                if (aimed && confirm.WasPressedThisFrame())
                {
                    placement.Dragging = true;
                    placement.LineStart = placement.Position;
                }

                return;
            }

            if (!confirm.WasReleasedThisFrame())
            {
                return;
            }

            if (placement.Valid)
            {
                AddCommand(new PlayerCommand
                {
                    Type = CommandType.PlaceLine,
                    Prefab = placement.Prefab,
                    Position = placement.LineStart,
                    End = placement.Position,
                    Queue = queue,
                });
            }

            // Shift keeps drawing lines of the same building.
            placement.Dragging = false;
            if (!queue)
            {
                placement = new PlacementState();
            }
        }

        private void AddCommand(in PlayerCommand command)
        {
            var player = SystemAPI.GetSingletonEntity<LocalPlayer>();
            EntityManager.GetBuffer<PlayerCommand>(player).Add(command);
        }

        private void Place(ref PlacementState placement, bool queue)
        {
            AddCommand(new PlayerCommand
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

        /// <summary>The ground under the cursor, unless the cursor is over the HUD.</summary>
        private bool TryAim(out float3 ground)
        {
            ground = default;
            var overUI = SystemAPI.GetSingleton<PointerState>().OverUI;
            return !overUI && _pointer.TryGround(_actions.Commands.Point.ReadValue<Vector2>(), out ground);
        }
    }
}
