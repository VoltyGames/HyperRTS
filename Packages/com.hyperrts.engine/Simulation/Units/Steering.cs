using HyperRTS.Simulation.Navigation;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;

namespace HyperRTS.Simulation.Units
{
    /// <summary>
    /// Sampled local steering for one mover. It scores the velocities its handling can reach within half a second
    /// against its neighbours' predicted capsules and the walls' distance field over the next 0.3, 0.75 and 1.5 s,
    /// plus how far each strays from the planned velocity and last frame's choice, and drives the cheapest.
    /// </summary>
    internal struct Steering
    {
        /// <summary>Longest look-ahead, which also sizes the neighbour query.</summary>
        public const float Horizon = 1.5f;

        // The look-ahead slices, so a sooner hit weighs more.
        private const float Soon = 0.3f;
        private const float Mid = 0.75f;
        private const float ReachTime = 0.5f;
        private const int Headings = 7;
        private const int Directions = 12;

        // Finer turns off the planned direction, so a small swerve needn't wait for the 30 degree ring.
        private const float Nudge = math.PI / 18f;

        // Backing off is the last resort, picked only when standing still is hit too.
        private const float ReverseCost = 0.3f;

        // Waiting is dearer than a slow swerve, so a pair stopped nose to nose sidesteps rather than deadlocks.
        private const float StandingCost = 0.25f;
        private const float Stickiness = 0.2f;

        // Breaks the tie of a head-on pair the same way for both, so each keeps right.
        private const float KeepRight = 0.05f;
        private const float Unchanged = 1e-4f;

        private static readonly ProfilerMarker PickMarker = new("Steering.Pick");

        private readonly FixedList512Bytes<AvoidanceNeighbour> _neighbours;
        private readonly float2 _position;
        private readonly float2 _axis;
        private readonly float _halfSegment;
        private readonly float _radius;
        private readonly float2 _velocity;
        private readonly float _topSpeed;
        private NavAccess _walls;
        private bool _hasWalls;
        private float2 _preferred;
        private float2 _last;
        private float _wallReach;
        private float2 _leg;

        public Steering(in CrowdVisitor crowd, float2 velocity, float topSpeed)
        {
            _neighbours = crowd.Neighbours;
            _position = crowd.Position;
            _axis = crowd.SegmentAxis;
            _halfSegment = math.length(crowd.SegmentAxis);
            _radius = crowd.Radius;
            _velocity = velocity;
            _topSpeed = math.max(topSpeed, 1e-3f);
            _walls = default;
            _hasWalls = false;
            _preferred = float2.zero;
            _last = float2.zero;
            _wallReach = 0f;
            _leg = float2.zero;
        }

        /// <summary>Also keeps the footprint off the walls of <paramref name="access"/>; aircraft have none.</summary>
        public void AddWalls(in NavAccess access)
        {
            _walls = access;
            _hasWalls = true;
        }

        /// <summary>
        /// Drives the leg, steering when the planned velocity would hit something. A manoeuvre runs at a crawl, and
        /// steering would break its legs.
        /// </summary>
        public float2 Drive(in Locomotion locomotion, ref LocomotionState drive, float2 facing, ref float2 heading,
            in PathLeg leg, float deltaTime)
        {
            var start = drive;
            heading = facing;
            var step = locomotion.Drive(ref drive, _position, ref heading, leg, _topSpeed, _radius, deltaTime);
            // With no one near, a pivot means to stand, not to roll on as it brakes.
            var standsToTurn = _neighbours.Length == 0 && locomotion.IsPivoting(drive, facing, leg);
            _preferred = standsToTurn ? float2.zero : step / deltaTime;
            if (drive.IsManoeuvring)
            {
                drive.LastChoice = _preferred;
                return step;
            }

            _last = start.LastChoice;
            _wallReach = leg.Distance;
            _leg = drive.Reverse ? -leg.Direction : leg.Direction;
            var chosen = Pick(locomotion, start, facing);
            if (math.distancesq(chosen, _preferred) > Unchanged)
            {
                drive = start;
                heading = facing;
                step = locomotion.Steer(ref drive, _position, ref heading, leg, chosen, _topSpeed, _radius, deltaTime);
            }

            drive.LastChoice = chosen;
            if (IsWedged(chosen, facing))
            {
                locomotion.BackOff(ref drive);
            }

            return step;
        }

        /// <summary>The cheapest candidate; the planned velocity itself when nothing is in its way.</summary>
        private readonly float2 Pick(in Locomotion locomotion, in LocomotionState state, float2 facing)
        {
            using var scope = PickMarker.Auto();
            var alone = _neighbours.Length == 0 && !IsNearWall();
            var penalty = alone ? 0f : Penalty(_preferred);
            if (penalty <= 0f)
            {
                return _preferred;
            }

            var best = new Choice { Velocity = _preferred, Cost = penalty + Straying(_preferred) };
            Try(ref best, _last, 0f);
            Try(ref best, float2.zero, StandingExtra(locomotion, facing));
            if (locomotion.IsInstant)
            {
                TryDirections(ref best, facing);
            }
            else
            {
                TryReachable(ref best, locomotion, state, facing);
            }

            return best.Velocity;
        }

        // Clear of traffic, tracks reach a leg beyond their reachable headings only by stopping to turn, so that isn't
        // waiting. Among neighbours standing still blocks them, so it keeps its cost.
        private readonly float StandingExtra(in Locomotion locomotion, float2 facing)
        {
            if (!locomotion.Pivots || _neighbours.Length > 0)
            {
                return StandingCost;
            }

            var reachable = locomotion.TurnWithin(_topSpeed * 0.5f, ReachTime);
            return math.dot(facing, _leg) < math.cos(reachable) ? 0f : StandingCost;
        }

        // Stopped short of a wall it is facing, which only backing off and turning gets it away from.
        private readonly bool IsWedged(float2 chosen, float2 facing)
        {
            if (math.lengthsq(chosen) > Unchanged)
            {
                return false;
            }

            var forward = facing * _topSpeed;
            var axis = facing * _halfSegment;
            return _hasWalls && WallDepth(forward, axis, Soon, DepthAt(_position, axis)) > 0f;
        }

        private readonly bool IsNearWall()
        {
            if (!_hasWalls)
            {
                return false;
            }

            var nearest = math.min(_walls.Distance(_position - _axis), _walls.Distance(_position + _axis));
            return nearest < _walls.WallMargin;
        }

        // Instant handling may take any direction at once, so the ring is turned to start on the planned one.
        private readonly void TryDirections(ref Choice best, float2 facing)
        {
            var forward = math.lengthsq(_preferred) > 1e-6f ? math.normalize(_preferred) : facing;
            for (var side = -2; side <= 2; side++)
            {
                Try(ref best, Rotate(_preferred, side * Nudge), 0f);
            }

            for (var i = 0; i < Directions; i++)
            {
                var direction = Rotate(forward, i * (math.PI * 2f / Directions));
                for (var speed = 1; speed <= 3; speed++)
                {
                    Try(ref best, direction * (_topSpeed * speed / 3f), 0f);
                }
            }
        }

        private readonly void TryReachable(ref Choice best, in Locomotion locomotion, in LocomotionState state,
            float2 facing)
        {
            if (!state.Reverse && locomotion.ReverseSpeed > 0f)
            {
                Try(ref best, -facing * locomotion.ReverseSpeed, ReverseCost);
            }

            var travel = state.Reverse ? -facing : facing;
            for (var share = 2; share <= 4; share++)
            {
                var speed = _topSpeed * share * 0.25f;
                var spread = locomotion.TurnWithin(speed, ReachTime);
                for (var i = 0; i < Headings; i++)
                {
                    var angle = spread * (i * 2f / (Headings - 1) - 1f);
                    Try(ref best, Rotate(travel, angle) * speed, 0f);
                }
            }
        }

        private readonly void Try(ref Choice best, float2 candidate, float extra)
        {
            // The penalty is never negative, so a candidate already dearer without it can't win.
            var cost = Straying(candidate) + extra;
            if (cost >= best.Cost)
            {
                return;
            }

            cost += Penalty(candidate);
            if (cost < best.Cost)
            {
                best = new Choice { Velocity = candidate, Cost = cost };
            }
        }

        private readonly float Straying(float2 candidate)
        {
            var left = math.max(0f, Cross(math.normalizesafe(_preferred), candidate));
            var straying = math.distance(candidate, _preferred) + Stickiness * math.distance(candidate, _last) +
                           KeepRight * left;
            return straying / _topSpeed;
        }

        /// <summary>Metres of overlap with neighbours and walls, weighted to count sooner hits more.</summary>
        private readonly float Penalty(float2 candidate)
        {
            var moving = _halfSegment > 0f && math.lengthsq(candidate) > 1e-6f;
            var axis = moving ? math.normalize(candidate) * _halfSegment : _axis;
            var resting = _hasWalls ? DepthAt(_position, axis) : 0f;
            return PenaltyAt(candidate, axis, resting, 0f, Soon) + PenaltyAt(candidate, axis, resting, Soon, Mid) +
                   PenaltyAt(candidate, axis, resting, Mid, Horizon);
        }

        private readonly float PenaltyAt(float2 candidate, float2 axis, float resting, float from, float time)
        {
            var overlap = 0f;
            foreach (var neighbour in _neighbours)
            {
                overlap += neighbour.Overlap(_position, axis, _velocity, candidate, from, time);
            }

            var walls = _hasWalls ? WallDepth(candidate, axis, time, resting) : 0f;
            return (overlap + walls) / time;
        }

        // Only as far as the next corner, since the path turns there and a straight line on would cross its wall.
        // Only sinking deeper counts, so a unit already touching a wall may still slide along it.
        private readonly float WallDepth(float2 candidate, float2 axis, float time, float resting)
        {
            var travel = math.min(math.length(candidate) * time, _wallReach);
            var center = _position + math.normalizesafe(candidate) * travel;
            return math.max(0f, DepthAt(center, axis) - resting);
        }

        private readonly float DepthAt(float2 center, float2 axis)
        {
            var open = _radius - NavTolerances.WallOverlap;
            return math.max(0f, open - _walls.Distance(center - axis)) +
                   math.max(0f, open - _walls.Distance(center + axis));
        }

        private static float2 Rotate(float2 direction, float angle)
        {
            math.sincos(angle, out var sin, out var cos);
            return new float2(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos);
        }

        private static float Cross(float2 a, float2 b) => a.x * b.y - a.y * b.x;

        private struct Choice
        {
            public float2 Velocity;
            public float Cost;
        }
    }
}
