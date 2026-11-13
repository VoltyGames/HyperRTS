using HyperRTS.Simulation.Common.Geometry;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Paths;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Navigation.Reach
{
    /// <summary>
    /// Picks where a unit stands to reach a footprint: of the open spots round it that no other unit has taken, the
    /// cheapest to drive to from its pose, and where it waits while all are taken.
    /// </summary>
    internal readonly struct StandSpotSearch
    {
        // Spots tried round the footprint, the first the nearest, so one the hull can drive straight to is found.
        private const int Spots = 16;

        // Cell rings searched round a spot for open ground the unit can reach.
        private const int StandRings = 2;

        // Spacings a unit waits off the footprint while every spot is held, past where a unit leaving one backs to.
        private const float WaitSpacings = 2f;

        private readonly NavAccess _access;
        private readonly bool _hasAccess;
        private readonly int _region;
        private readonly NavBox _footprint;
        private readonly NavAgent _agent;
        private readonly float2 _position;
        private readonly float2 _facing;
        private readonly float _offset;

        public StandSpotSearch(in NavGrid grid, in NavBox footprint, in LocalTransform unit, in NavAgent agent)
        {
            _hasAccess = grid.IsCreated && grid.Classes.Find(agent.Class) >= 0;
            _access = _hasAccess ? agent.Access(grid) : default;
            _region = _hasAccess ? _access.NearestRegion(unit.Position) : 0;
            _footprint = footprint;
            _agent = agent;
            _position = unit.Position.xz;
            _facing = math.normalizesafe(unit.Forward().xz);
            // The hull's length off the footprint, so it fits there at any heading.
            _offset = agent.HalfLength + TargetReach.Slack * 0.5f;
        }

        /// <summary>
        /// How far apart units take spots: a hull's length and width, so neighbours keep a gap at any heading, nose to
        /// tail or side by side.
        /// </summary>
        public static float Spacing(in NavAgent agent) => (agent.HalfLength + agent.Radius) * 2f;

        /// <summary>The point the hull's length off the footprint nearest the unit.</summary>
        public float2 Nearest => _footprint.EdgePoint(_position, _offset);

        /// <summary>How far off the footprint a unit waits for a spot.</summary>
        public static float WaitGap(in NavAgent agent) => Spacing(agent) * WaitSpacings;

        /// <summary>Where the unit waits for a spot: off the footprint's nearest edge by <see cref="WaitGap"/>.</summary>
        public float2 WaitPoint => _footprint.EdgePoint(_position, WaitGap(_agent));

        /// <summary>
        /// The cheapest open spot no other unit at <paramref name="target"/> holds, so units spread round it; false
        /// when every open spot is held. With no open spot at all it is the nearest, as walls may yet open.
        /// </summary>
        public bool TryFind(Entity target, in ReachClaims claims, out float2 best)
        {
            best = Nearest;
            var bestCost = float.MaxValue;
            var spacing = Spacing(_agent);
            var far = math.cmax(_footprint.Half) * 4f + _offset;
            var anyOpen = false;
            for (var i = 0; i < Spots; i++)
            {
                var around = Planar.Rotate(_footprint.Right, i * (math.PI * 2f / Spots));
                var spot = i == 0 ? Nearest : _footprint.EdgePoint(_footprint.Center + around * far, _offset);
                var cost = DriveCost(spot);
                if (cost >= bestCost || !TryOpen(spot, out var open))
                {
                    continue;
                }

                anyOpen = true;
                if (!claims.IsTaken(target, open, spacing))
                {
                    best = open;
                    bestCost = cost;
                }
            }

            return !anyOpen || bestCost < float.MaxValue;
        }

        // The straight line plus the arc that turns the hull onto it, forwards or, on a short way behind, backing.
        // A spot past the footprint is driven to round it, never backed onto in a line, and a spot inside a turning
        // circle lies on no arc, so each costs its detour.
        private float DriveCost(float2 spot)
        {
            var to = spot - _position;
            var distance = math.length(to);
            var along = math.dot(_facing, to) / math.max(distance, 1e-3f);
            var past = _footprint.Gap(_position, spot) < 1e-3f;
            var shortWay = !past && distance <= PathRequest.ShortWayHulls * _agent.HullLength;
            var backs = _agent.CanReverse && shortWay && along < PathRequest.BehindCos;
            var turn = math.acos(math.clamp(backs ? -along : along, -1f, 1f));
            var chassis = _agent.Chassis;
            var round = past ? math.csum(_footprint.Half) * 2f : 0f;
            var circle = chassis.IsInsideTurn(_position, _facing, spot, 1f) ? math.PI * 2f * chassis.TurnRadius : 0f;
            return distance + turn * chassis.TurnLength(_agent.HalfLength) + round + circle;
        }

        // Open ground in the unit's region at or near the spot.
        private bool TryOpen(float2 spot, out float2 open)
        {
            open = spot;
            if (!_hasAccess)
            {
                return true;
            }

            var cell = _access.Grid.Grid.Cell(new float3(spot.x, 0f, spot.y));
            if (_region == 0 || !_access.TryFindNearest(cell, StandRings, _region, out var found))
            {
                return false;
            }

            open = found.Equals(cell) ? spot : _access.Grid.Grid.Center(found);
            return true;
        }
    }
}
