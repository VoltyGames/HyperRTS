using HyperRTS.Simulation.Common.Cells;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Movement;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Spatial;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Squads
{
    /// <summary>
    /// Where a squad's members may stand this frame: off the hulls of allied vehicles round it and on cells open to
    /// the squad's class, a place past a wall cut back toward the squad's centre.
    /// </summary>
    internal struct SquadGround : ISpatialVisitor
    {
        // A member keeps its own width and a little more off a hull's edge.
        private const float HullMargin = SquadMember.Radius + 0.1f;

        // Back from a closed cell's edge, so a cut place lies in the open cell before it.
        private const float EdgeBack = 0.01f;

        // The field is read at cell centres, each up to a diagonal from the points it stands for.
        private const float CellSlack = 2f;

        private readonly NavAccess _access;
        private readonly bool _hasAccess;
        private readonly float2 _centre;
        private readonly float _openReach;
        private readonly Entity _squad;
        private readonly byte _owner;
        private readonly OwnerRelations _relations;
        private readonly float _deltaTime;
        private FixedList512Bytes<Hull> _hulls;

        public SquadGround(in NavGrid grid, in NavAgent agent, float2 centre, Entity squad, byte owner,
            in OwnerRelations relations, float deltaTime)
        {
            _hasAccess = agent.TryAccess(grid, out _access);
            _centre = centre;
            _squad = squad;
            _owner = owner;
            _relations = relations;
            _deltaTime = deltaTime;
            _hulls = default;
            // Within this of the centre every cell is open, so most places need no ray.
            _openReach = _hasAccess ? OpenReach(_access, centre) : float.PositiveInfinity;
        }

        /// <summary>Gathers the allied vehicle hulls within <paramref name="spread"/> of the centre.</summary>
        public void FindHulls(in SpatialIndex index, float spread) =>
            index.QueryHulls(new float3(_centre.x, 0f, _centre.y), spread + HullMargin, ref this);

        public void Visit(in SpatialEntry entry)
        {
            if (!IsAlliedVehicle(entry) || _hulls.Length == _hulls.Capacity)
            {
                return;
            }

            // The index holds where the vehicle started this frame; movement has carried it on since.
            var centre = entry.Position.xz + entry.Velocity * _deltaTime;
            _hulls.Add(new Hull(centre, entry.SegmentAxis, entry.Facing, entry.Radius + HullMargin));
        }

        /// <summary>The offset from the centre nearest <paramref name="offset"/> that is off every hull and open.</summary>
        public readonly float2 Place(float2 offset) => CutAtWalls(OffHulls(offset));

        private readonly bool IsAlliedVehicle(in SpatialEntry entry)
        {
            if (!entry.IsVehicle || entry.Layer == NavLayer.Air || entry.Entity == _squad)
            {
                return false;
            }

            return _relations.IsAllied(_owner, entry.Owner);
        }

        private readonly float2 OffHulls(float2 offset)
        {
            var point = _centre + offset;
            foreach (var hull in _hulls)
            {
                point = hull.Outside(point, _centre);
            }

            return point - _centre;
        }

        private readonly float2 CutAtWalls(float2 offset)
        {
            var length = math.length(offset);
            return length <= _openReach ? offset : offset * OpenShare(_centre + offset, length);
        }

        // The share of the line from the centre to the place that stays on open cells, corners squeezed like a path.
        private readonly float OpenShare(float2 place, float length)
        {
            var ray = new GridRay(_access.Grid.Grid, _centre, place);
            ray.MoveNext();
            if (!_access.IsOpen(ray.Cell))
            {
                return 0f;
            }

            while (ray.MoveNext())
            {
                if (!_access.CanCross(ray))
                {
                    return math.max(0f, ray.Entry - EdgeBack / length);
                }
            }

            return 1f;
        }

        private static float OpenReach(in NavAccess access, float2 centre)
        {
            var cell = access.Grid.Grid.Cell(centre);
            if (!access.Grid.Grid.Contains(cell))
            {
                return 0f;
            }

            return access.Distance(cell) - access.OpenDistance - CellSlack * access.Grid.Grid.CellSize;
        }

        // A vehicle's footprint as a capsule round its core segment, widened by the member's margin.
        private readonly struct Hull
        {
            private readonly float2 _centre;
            private readonly float2 _axis;
            private readonly float2 _facing;
            private readonly float _reach;

            public Hull(float2 centre, float2 axis, float2 facing, float reach)
            {
                _centre = centre;
                _axis = axis;
                _facing = facing;
                _reach = reach;
            }

            // A point under the hull moves straight out to the nearer side; one on its axis to the squad's side.
            public float2 Outside(float2 point, float2 squad)
            {
                var span = math.lengthsq(_axis);
                var along = span > 0f ? math.clamp(math.dot(point - _centre, _axis) / span, -1f, 1f) : 0f;
                var closest = _centre + _axis * along;
                var away = point - closest;
                var distance = math.length(away);
                if (distance >= _reach)
                {
                    return point;
                }

                var side = distance > EdgeBack ? away / distance : RightOfWay.Side(_facing, squad - _centre);
                return closest + side * _reach;
            }
        }
    }
}
