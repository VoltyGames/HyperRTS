using HyperRTS.Input.Commands;
using HyperRTS.Presentation.Interaction;
using HyperRTS.Simulation.Buildings.Placement;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Orders;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HyperRTS.Input.Placement
{
    /// <summary>
    /// Drives building placement mode: moves the snapped ghost, turns it through <see cref="PlacementRotation"/> and
    /// confirms it on release as a PlaceBuilding command. A line prefab draws a wall instead: a click starts it, each
    /// further click lays a leg as a PlaceLine command and turns a corner there, and Command finishes it.
    /// </summary>
    // After CommandInputSystem, so a cancelling right-click can't also become a smart command this frame.
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(CommandInputSystem))]
    public partial class PlacementInputSystem : InputMapSystem
    {
        // Metres the cursor moves from the press before a held click turns the ghost, so a plain click never does.
        private const float DragDistance = 2f;

        private MatchInputActions _actions;
        private WorldPointer _pointer;
        private PlacementRotation _rotation;
        private bool _keysPublished;
        private SeenEnemies _enemies;
        private VisibleFootprints _footprints;


        protected override void OnCreate()
        {
            _actions = InputActionsProvider.Actions;
            _pointer = new WorldPointer(ref CheckedStateRef);
            _rotation = PlacementRotation.Start();
            _enemies = new SeenEnemies(ref CheckedStateRef);
            _footprints = new VisibleFootprints(ref CheckedStateRef);
            RequireForUpdate<PlacementState>();
            RequireForUpdate<ScrollEdge>();
            RequireForUpdate<PointerState>();
            RequireForUpdate<PlacementKeys>();
            RequireForUpdate<LocalPlayer>();
            RequireForUpdate<MapSettings>();
            RequireForUpdate<NavGrid>();
            RequireForUpdate<OwnerRelations>();
        }

        protected override InputActionMap Map => _actions.Commands;

        protected override void OnUpdate()
        {
            var placement = SystemAPI.GetSingleton<PlacementState>();
            if (!placement.Active)
            {
                _keysPublished = false;
                _sent = false;
                _rotation = PlacementRotation.Start();
                PauseEdgeScroll(false);
                return;
            }

            var commands = _actions.Commands;
            PublishKeys(commands);
            var cancelled = commands.Command.WasPressedThisFrame();
            if (placement.Prefab == Entity.Null || (cancelled && !placement.Drawing))
            {
                SystemAPI.SetSingleton(new PlacementState());
                return;
            }

            Move(ref placement, commands);
            PauseEdgeScroll(_rotation.IsTurning);
            SystemAPI.SetSingleton(placement);
        }

        private void Move(ref PlacementState placement, MatchInputActions.CommandsActions commands)
        {
            var cancelled = commands.Command.WasPressedThisFrame();
            var aimed = TryAim(out var ground);
            // Shift queues the placement and snaps its turn and line direction.
            var shift = commands.Queue.IsPressed();
            if (SystemAPI.HasComponent<LinePlacement>(placement.Prefab))
            {
                // Command finishes the wall and leaves placement on for the next one.
                placement.Drawing &= !cancelled;
                // A leg released over the HUD still ends where the cursor last touched the ground.
                if (aimed)
                {
                    MoveLine(ref placement, ground, shift);
                }

                DrawWall(ref placement, aimed, shift);
            }
            else
            {
                Rotate(commands);
                MoveSingle(ref placement, aimed, ground, shift);
            }
        }

        // Read once per placement so a rebound key shows on the next one.
        private void PublishKeys(MatchInputActions.CommandsActions commands)
        {
            if (_keysPublished)
            {
                return;
            }

            _keysPublished = true;
            SystemAPI.SetSingleton(new PlacementKeys
            {
                Place = commands.Confirm.GetBindingDisplayString(),
                Rotate = commands.RotateBuilding.GetBindingDisplayString(),
                Snap = commands.Queue.GetBindingDisplayString(),
                Finish = commands.Command.GetBindingDisplayString(),
            });
        }

        /// <summary>RotateBuilding held turns with the pointer's sideways motion; a tap adds a step.</summary>
        private void Rotate(MatchInputActions.CommandsActions commands)
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
            if (aimed && math.lengthsq(pull) > DragDistance * DragDistance)
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
            using var footprints = _footprints.Read(EntityManager);
            var clear = spot.CanPlace(map, grid, footprints.AsArray(), inLine: false);
            placement.Valid = clear && !_enemies.Block(spot);
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

        /// <summary>The prefab's unsnapped spot and what snapping it reads; complete jobs before reading cells.</summary>
        private BuildingSpot Spot(Entity prefab, float3 position, float yaw, out MapSettings map, out NavGrid grid)
        {
            map = SystemAPI.GetSingleton<MapSettings>();
            grid = SystemAPI.GetSingleton<NavGrid>();
            var surface = BuildingPlacement.GetSurface(EntityManager, prefab);
            return new BuildingSpot(position, yaw, SystemAPI.GetComponent<NavObstacle>(prefab).Size, surface);
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