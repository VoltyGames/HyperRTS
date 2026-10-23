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
    /// Drives building placement mode: moves and turns the snapped ghost and confirms it as a PlaceBuilding command, or
    /// for a line prefab drags the line from press to release and sends it as a PlaceLine command.
    /// </summary>
    // After CommandInputSystem, so a cancelling right-click can't also become a smart command this frame.
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(CommandInputSystem))]
    public partial class PlacementInputSystem : SystemBase
    {
        private RTSInputActions _actions;
        private WorldPointer _pointer;

        // The line last planned into the PlacementSegment buffer, so a still cursor doesn't replan it.
        private PlacementLine _line;
        private Entity _linePrefab;
        private int _lineGridVersion;

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

            if (commands.RotateBuilding.WasPressedThisFrame())
            {
                placement.Turn = (byte)((placement.Turn + 1) % PlacementMath.Turns);
                SystemAPI.SetSingleton(placement);
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
            var size = ReadFootprint(placement.Prefab, out var map, out var grid, out var surface);
            var footprint = PlacementMath.Turned(size, placement.Turn);
            CompleteDependency();
            placement.Valid = PlacementMath.Resolve(in map, in grid, ground, footprint, surface, out placement.Position);
        }

        private void MoveLine(ref PlacementState placement, float3 ground)
        {
            var size = ReadFootprint(placement.Prefab, out var map, out var grid, out var surface);
            placement.Position = ground;
            var start = placement.Dragging ? placement.LineStart : ground;
            var line = new PlacementLine(map, start, ground, size);
            var segments = SystemAPI.GetSingletonBuffer<PlacementSegment>();
            if (!IsPlanned(line, placement.Prefab, grid.Version))
            {
                CompleteDependency();
                segments.Clear();
                for (var i = 0; i < line.Count; i++)
                {
                    var footprint = line.Footprint;
                    var valid = PlacementMath.Resolve(map, grid, line.Segment(i), footprint, surface, out var center);
                    segments.Add(new PlacementSegment { Center = center, Footprint = footprint, Valid = valid });
                }

                _line = line;
                _linePrefab = placement.Prefab;
                _lineGridVersion = grid.Version;
            }

            // Read back from the buffer: a restarted placement resets Valid over an unchanged line.
            placement.Valid = PlacementSegment.CountValid(segments) > 0;
        }

        private bool IsPlanned(in PlacementLine line, Entity prefab, int gridVersion) =>
            line.Matches(_line) && prefab == _linePrefab && gridVersion == _lineGridVersion;

        /// <summary>The prefab's unturned footprint and what snapping it reads; complete jobs before reading cells.</summary>
        private float2 ReadFootprint(Entity prefab, out MapSettings map, out NavGrid grid, out PlacementSurface surface)
        {
            map = SystemAPI.GetSingleton<MapSettings>();
            grid = SystemAPI.GetSingleton<NavGrid>();
            surface = BuildingPlacement.GetSurface(EntityManager, prefab);
            return SystemAPI.GetComponent<NavObstacle>(prefab).Size;
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
                Turn = placement.Turn,
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
