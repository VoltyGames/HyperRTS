using HyperRTS.Presentation.Interaction;
using HyperRTS.Simulation.Buildings.Placement;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Orders;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Input.Placement
{
    public partial class PlacementInputSystem
    {
        // The line last planned into the PlacementSegment buffer, so a still cursor doesn't replan it.
        private PlacementLine _line;
        private Entity _linePrefab;
        private int _lineGridVersion;

        // The wall being drawn: the leg laid last, which the next one joins at a corner; empty before the first.
        private PlacementLine _laid;

        // A leg went out this placement, so the next ones queue behind it instead of replacing the builders' orders.
        private bool _sent;

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
                    var clear = spot.CanPlace(map, grid, footprints.AsArray(), inLine: true);
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
        /// Sends the leg as planned and joins the next one to its end. The placement's first leg queues with Shift;
        /// every later leg, on this wall or the next, always queues, so the builders work through them in order.
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
    }
}
