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

        // The line last planned into the PlacementSegment buffer, so a still cursor doesn't replan it.
        private PlacementLine _line;
        private Entity _linePrefab;
        private int _lineGridVersion;

        // The wall being drawn: the leg laid last, which the next one joins at a corner; empty before the first.
        private PlacementLine _laid;

        // A leg went out this placement, so the next ones queue behind it instead of replacing the builders' orders.
        private bool _sent;

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

            PauseEdgeScroll(_rotation.IsTurning);
            SystemAPI.SetSingleton(placement);
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
            var clear = spot.CanPlace(map, grid, footprints.AsArray(), false);
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

        /// <summary>
        /// Plans the leg being drawn into the PlacementSegment buffer: from the wall's start, or past the corner where
        /// the last leg ended; before a wall starts, one segment at the cursor.
        /// </summary>
        private void MoveLine(ref PlacementState placement, float3 ground, bool shift)
        {
            var line = Leg(ref placement, ground, shift, out var map, out var grid);
            var segments = SystemAPI.GetSingletonBuffer<PlacementSegment>();
            if (!IsPlanned(line, placement.Prefab, grid.Version))
            {
                CompleteDependency();
                segments.Clear();
                using var footprints = _footprints.Read(EntityManager);
                for (var i = 0; i < line.Count; i++)
                {
                    var spot = line.Segment(i).Grounded(grid);
                    var clear = spot.CanPlace(map, grid, footprints.AsArray(), true);
                    segments.Add(new PlacementSegment { Spot = spot, Ground = clear });
                }

                _line = line;
                _linePrefab = placement.Prefab;
                _lineGridVersion = grid.Version;
            }

            _enemies.Mark(segments);
            placement.Valid = PlacementSegment.CountValid(segments) > 0;
        }

        /// <summary>
        /// The leg to the cursor, Shift snapping its direction, with the placement's
        /// <see cref="PlacementState.DragStart"/> and <see cref="PlacementState.Position"/> set to its start and end.
        /// </summary>
        private PlacementLine Leg(ref PlacementState placement, float3 ground, bool shift, out MapSettings map,
            out NavGrid grid)
        {
            var first = Spot(placement.Prefab, ground, 0f, out map, out grid);
            if (!placement.Drawing)
            {
                placement.Position = ground;
                return new PlacementLine(map, ground, ground, first.Footprint, first.Surface, LineStart.Snapped);
            }

            if (_laid.Count == 0)
            {
                placement.Position = shift ? PlacementRotation.SnapEnd(placement.DragStart, ground) : ground;
                return new PlacementLine(map, placement.DragStart, placement.Position, first.Footprint, first.Surface,
                    LineStart.Snapped);
            }

            // A joined leg turns at the corner, so its direction is measured from there.
            var corner = _laid.Joint;
            var aim = shift ? PlacementRotation.SnapEnd(corner, ground) : ground;
            var direction = math.normalizesafe(aim.xz - corner.xz, math.normalize(_laid.Step.xz));
            var start = _laid.JoinedStart(direction);
            // Never behind the start, which would turn the leg back over the corner.
            var reach = math.max(math.dot(aim.xz - start.xz, direction), 0.01f);
            var end = start.xz + direction * reach;
            placement.DragStart = start;
            placement.Position = new float3(end.x, aim.y, end.y);
            return new PlacementLine(map, start, placement.Position, first.Footprint, first.Surface, LineStart.Joined);
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

        /// <summary>
        /// A click starts a wall at the cursor. While drawing, a click lays the leg and turns a corner there, so the
        /// next leg runs on from its end.
        /// </summary>
        private void DrawWall(ref PlacementState placement, bool aimed, bool shift)
        {
            if (!aimed || !_actions.Commands.Confirm.WasPressedThisFrame())
            {
                return;
            }

            if (placement.Drawing)
            {
                LayLeg(ref placement, shift);
                return;
            }

            placement.Drawing = true;
            placement.DragStart = placement.Position;
            _laid = default;
        }

        /// <summary>
        /// Sends the leg as planned and joins the next one to its end. The first leg queues with Shift; the rest always
        /// queue, so the builders work through every wall drawn in order.
        /// </summary>
        private void LayLeg(ref PlacementState placement, bool shift)
        {
            if (!placement.Valid)
            {
                return;
            }

            AddCommand(new PlayerCommand
            {
                Type = CommandType.PlaceLine,
                Prefab = placement.Prefab,
                Position = placement.DragStart,
                End = placement.Position,
                Queue = shift || _sent,
                Argument = (int)(_laid.Count > 0 ? LineStart.Joined : LineStart.Snapped),
            });
            _sent = true;
            _laid = _line;
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
