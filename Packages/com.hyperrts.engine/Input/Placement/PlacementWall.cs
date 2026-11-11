using HyperRTS.Presentation.Interaction;
using HyperRTS.Simulation.Buildings.Placement;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Orders;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Input.Placement
{
    /// <summary>The wall being drawn: the leg planned for the cursor, the leg laid last and whether a leg went out yet.</summary>
    internal sealed class PlacementWall
    {
        // The line last planned into the PlacementSegment buffer, so a still cursor doesn't replan it.
        private PlacementLine _planned;
        private Entity _plannedPrefab;
        private int _plannedGridVersion;

        // The leg laid last, which the next one joins at a corner; empty before the first.
        private PlacementLine _laid;

        // A leg went out this placement, so the next ones queue behind it instead of replacing the builders' orders.
        private bool _sent;

        /// <summary>A new placement queues its first leg only with Shift again.</summary>
        public void Reset() => _sent = false;

        public bool IsPlanned(in PlacementLine line, Entity prefab, int gridVersion) =>
            line.Matches(_planned) && prefab == _plannedPrefab && gridVersion == _plannedGridVersion;

        public void Plan(in PlacementLine line, Entity prefab, int gridVersion)
        {
            _planned = line;
            _plannedPrefab = prefab;
            _plannedGridVersion = gridVersion;
        }

        /// <summary>
        /// The leg to the cursor, Shift snapping its direction, with the placement's
        /// <see cref="PlacementState.DragStart"/> and <see cref="PlacementState.Position"/> set to its start and end.
        /// <paramref name="first"/> is the prefab's unsnapped spot at the cursor.
        /// </summary>
        public PlacementLine Leg(ref PlacementState placement, float3 ground, bool shift, in MapSettings map,
            in BuildingSpot first)
        {
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

        /// <summary>
        /// A click starts a wall at the cursor. While drawing, a click lays the planned leg and turns a corner there, so
        /// the next leg runs on from its end. The placement's first leg queues with Shift; every later leg, on this wall
        /// or the next, always queues, so the builders work through them in order.
        /// </summary>
        public bool TryClick(ref PlacementState placement, bool shift, out PlayerCommand leg)
        {
            leg = default;
            if (!placement.Drawing)
            {
                placement.Drawing = true;
                placement.DragStart = placement.Position;
                _laid = default;
                return false;
            }

            if (!placement.Valid)
            {
                return false;
            }

            leg = new PlayerCommand
            {
                Type = CommandType.PlaceLine,
                Prefab = placement.Prefab,
                Position = placement.DragStart,
                End = placement.Position,
                Queue = shift || _sent,
                Argument = (int)(_laid.Count > 0 ? LineStart.Joined : LineStart.Snapped),
            };
            _sent = true;
            _laid = _planned;
            return true;
        }
    }
}
