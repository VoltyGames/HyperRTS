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
    /// Drives building placement mode: moves the snapped ghost, turns it through <see cref="PlacementRotation"/> and
    /// confirms it on release as a PlaceBuilding command, or for a line prefab drags the line as a PlaceLine command.
    /// </summary>
    // After CommandInputSystem, so a cancelling right-click can't also become a smart command this frame.
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(CommandInputSystem))]
    public partial class PlacementInputSystem : SystemBase
    {
        // Metres the cursor moves from the press before a held click turns the ghost, so a plain click never does.
        private const float TurnDragDistance = 2f;

        private RTSInputActions _actions;
        private WorldPointer _pointer;
        private PlacementRotation _rotation;
        private SeenEnemies _enemies;

        // The line last planned into the PlacementSegment buffer, so a still cursor doesn't replan it.
        private PlacementLine _line;
        private Entity _linePrefab;
        private int _lineGridVersion;

        protected override void OnCreate()
        {
            _actions = InputActionsProvider.Actions;
            _pointer = new WorldPointer(ref CheckedStateRef);
            _rotation = PlacementRotation.Start();
            _enemies = new SeenEnemies(ref CheckedStateRef);
            RequireForUpdate<PlacementState>();
            RequireForUpdate<ScrollEdge>();
            RequireForUpdate<PointerState>();
            RequireForUpdate<LocalPlayer>();
            RequireForUpdate<MapSettings>();
            RequireForUpdate<NavGrid>();
            RequireForUpdate<FactionRelations>();
        }

        // Not in OnCreate: without domain reload the Input System wipes action states after the world is created.
        protected override void OnStartRunning() => InputActionsProvider.Enable(_actions.Commands);

        protected override void OnStopRunning() => InputActionsProvider.Disable(_actions.Commands);

        protected override void OnUpdate()
        {
            var placement = SystemAPI.GetSingleton<PlacementState>();
            if (!placement.Active)
            {
                _rotation = PlacementRotation.Start();
                PauseEdgeScroll(false);
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
            // Shift queues the placement and snaps its turn and line direction.
            var shift = commands.Queue.IsPressed();
            if (SystemAPI.HasComponent<LinePlacement>(placement.Prefab))
            {
                // A drag released over the HUD still ends where the cursor last touched the ground.
                if (aimed)
                {
                    MoveLine(ref placement, ground, shift);
                }

                DragLine(ref placement, aimed, shift);
            }
            else
            {
                Rotate(commands);
                MoveSingle(ref placement, aimed, ground, shift);
            }

            PauseEdgeScroll(_rotation.IsTurning);
            SystemAPI.SetSingleton(placement);
        }

        /// <summary>RotateBuilding held turns with the pointer's sideways motion; a tap adds a step.</summary>
        private void Rotate(RTSInputActions.CommandsActions commands)
        {
            var pointerX = commands.Point.ReadValue<Vector2>().x;
            var key = commands.RotateBuilding;
            if (key.WasPressedThisFrame())
            {
                _rotation.Press(pointerX);
            }
            else if (_rotation.IsTurning)
            {
                _rotation.Turn(pointerX);
            }

            if (key.WasReleasedThisFrame() && _rotation.IsTurning)
            {
                _rotation.Release();
            }
        }

        /// <summary>The ghost follows the cursor, stays pinned while dragged or turned, and places on release.</summary>
        private void MoveSingle(ref PlacementState placement, bool aimed, float3 ground, bool shift)
        {
            if (placement.Dragging)
            {
                DragTurn(ref placement, aimed, ground, shift);
            }
            else if (_rotation.IsTurning)
            {
                MoveGhost(ref placement, placement.DragStart, shift);
            }
            else if (aimed)
            {
                MoveGhost(ref placement, ground, shift);
                placement.Dragging = _actions.Commands.Confirm.WasPressedThisFrame();
                placement.DragStart = ground;
            }
        }

        /// <summary>Holding Confirm pins the ghost and turns its front toward the cursor; release places it.</summary>
        private void DragTurn(ref PlacementState placement, bool aimed, float3 ground, bool shift)
        {
            var pull = ground.xz - placement.DragStart.xz;
            if (aimed && math.lengthsq(pull) > TurnDragDistance * TurnDragDistance)
            {
                _rotation.Aim(pull);
            }

            MoveGhost(ref placement, placement.DragStart, shift);
            if (!_actions.Commands.Confirm.WasReleasedThisFrame())
            {
                return;
            }

            placement.Dragging = false;
            if (placement.Valid)
            {
                Place(ref placement, shift);
            }
        }

        private void MoveGhost(ref PlacementState placement, float3 ground, bool shift)
        {
            var asked = Spot(placement.Prefab, ground, _rotation.Yaw(shift), out var map, out var grid);
            CompleteDependency();
            var spot = asked.Snap(map).Grounded(grid);
            placement.Position = spot.Center;
            placement.Yaw = spot.Yaw;
            placement.Valid = spot.IsValid(map, grid) && !_enemies.Block(spot);
        }

        private void PauseEdgeScroll(bool paused)
        {
            var edge = SystemAPI.GetSingleton<ScrollEdge>();
            if (edge.Paused != paused)
            {
                edge.Paused = paused;
                SystemAPI.SetSingleton(edge);
            }
        }

        private void MoveLine(ref PlacementState placement, float3 ground, bool shift)
        {
            var start = placement.Dragging ? placement.DragStart : ground;
            placement.Position = shift ? PlacementRotation.SnapEnd(start, ground) : ground;
            var first = Spot(placement.Prefab, start, 0f, out var map, out var grid);
            var line = new PlacementLine(map, start, placement.Position, first.Footprint, first.Surface);
            var segments = SystemAPI.GetSingletonBuffer<PlacementSegment>();
            if (!IsPlanned(line, placement.Prefab, grid.Version))
            {
                CompleteDependency();
                segments.Clear();
                for (var i = 0; i < line.Count; i++)
                {
                    var spot = line.Segment(i).Grounded(grid);
                    segments.Add(new PlacementSegment { Spot = spot, Ground = spot.IsValid(map, grid) });
                }

                _line = line;
                _linePrefab = placement.Prefab;
                _lineGridVersion = grid.Version;
            }

            _enemies.Mark(segments);
            placement.Valid = PlacementSegment.CountValid(segments) > 0;
        }

        private bool IsPlanned(in PlacementLine line, Entity prefab, int gridVersion) =>
            line.Matches(_line) && prefab == _linePrefab && gridVersion == _lineGridVersion;

        /// <summary>The prefab's unsnapped spot and what snapping it reads; complete jobs before reading cells.</summary>
        private BuildingSpot Spot(Entity prefab, float3 position, float yaw, out MapSettings map, out NavGrid grid)
        {
            map = SystemAPI.GetSingleton<MapSettings>();
            grid = SystemAPI.GetSingleton<NavGrid>();
            var surface = BuildingPlacement.GetSurface(EntityManager, prefab);
            return new BuildingSpot(position, yaw, SystemAPI.GetComponent<NavObstacle>(prefab).Size, surface);
        }

        private void DragLine(ref PlacementState placement, bool aimed, bool queue)
        {
            var confirm = _actions.Commands.Confirm;
            if (!placement.Dragging)
            {
                if (aimed && confirm.WasPressedThisFrame())
                {
                    placement.Dragging = true;
                    placement.DragStart = placement.Position;
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
                    Position = placement.DragStart,
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
                Yaw = placement.Yaw,
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
