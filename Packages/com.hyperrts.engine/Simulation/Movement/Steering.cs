using HyperRTS.Simulation.Common.Geometry;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Drive;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Mathematics;
using Unity.Profiling;

namespace HyperRTS.Simulation.Movement
{
    /// <summary>
    /// Picks one mover's velocity from those its handling can reach, scoring each by how deep it would run into
    /// neighbours and walls and how far it strays from the plan.
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

        // Waiting is dearer than a slow swerve, so a pair stopped nose to nose sidesteps rather than deadlocks.
        private const float StandingCost = 0.25f;
        private const float Stickiness = 0.2f;

        // Breaks the tie of a head-on pair the same way for both, so each keeps right.
        private const float KeepRight = 0.05f;
        private const float Unchanged = 1e-4f;

        private static readonly ProfilerMarker PickMarker = new("Steering.Pick");

        private readonly Neighbourhood _crowd;
        private readonly float _halfSegment;
        private WallProbe _walls;
        private float2 _preferred;
        private float2 _last;
        private float2 _leg;

        // The driver before this frame's drive, whose handling bounds the candidates.
        private Driver _start;

        public Steering(in CrowdVisitor crowd, float2 velocity)
        {
            _crowd = new Neighbourhood(crowd, velocity);
            _halfSegment = math.length(crowd.SegmentAxis);
            _walls = new WallProbe(crowd.Position, crowd.Radius);
            _preferred = float2.zero;
            _last = float2.zero;
            _leg = float2.zero;
            _start = default;
        }

        /// <summary>Also keeps the footprint off the walls of <paramref name="access"/>; aircraft have none.</summary>
        public void AddWalls(in NavAccess access) => _walls.Add(access);

        /// <summary>
        /// Drives the leg, steering when the planned velocity would hit something. A planned manoeuvre isn't steered,
        /// which would break its legs; it waits instead.
        /// </summary>
        public float2 Drive(ref Driver driver, in PathLeg leg) =>
            leg.Tight ? DriveTight(ref driver, leg) : DriveSampled(ref driver, leg);

        // Stands while driving on would run deeper into a neighbour than standing would. One held too long replans,
        // so two waiting on each other can't deadlock.
        private float2 DriveTight(ref Driver driver, in PathLeg leg)
        {
            var start = driver;
            var step = driver.Drive(leg);
            if (RunsInto(step / driver.DeltaTime))
            {
                driver = start;
                step = driver.Steer(leg, float2.zero);
            }

            driver.State.LastChoice = step / driver.DeltaTime;
            return step;
        }

        private readonly bool RunsInto(float2 velocity) =>
            _crowd.Overlap(_crowd.Axis, velocity, 0f, Horizon) >
            _crowd.Overlap(_crowd.Axis, float2.zero, 0f, Horizon) + Unchanged;

        // A reflex manoeuvre runs at a crawl, and steering would break its legs.
        private float2 DriveSampled(ref Driver driver, in PathLeg leg)
        {
            _start = driver;
            var step = driver.Drive(leg);
            // With no one near, a pivot means to stand, not to roll on as it brakes.
            var standsToTurn = _crowd.Count == 0 && driver.IsPivoting(_start.Facing, leg);
            _preferred = standsToTurn ? float2.zero : step / driver.DeltaTime;
            if (driver.State.IsManoeuvring)
            {
                driver = _start;
                return DriveTight(ref driver, leg);
            }

            _last = _start.State.LastChoice;
            _walls.Reach = leg.Distance;
            _leg = driver.State.Reverse ? -leg.Direction : leg.Direction;
            var chosen = Pick();
            if (math.distancesq(chosen, _preferred) > Unchanged)
            {
                driver = _start;
                step = driver.Steer(leg, chosen);
            }

            driver.State.LastChoice = chosen;
            if (IsWedged(chosen))
            {
                driver.BackOff();
            }

            return step;
        }

        /// <summary>The cheapest candidate; the planned velocity itself when nothing is in its way.</summary>
        private readonly float2 Pick()
        {
            using var scope = PickMarker.Auto();
            var alone = _crowd.Count == 0 && !_walls.IsNear(_crowd.Axis);
            var penalty = alone ? 0f : Penalty(_preferred);
            if (penalty <= 0f)
            {
                return _preferred;
            }

            var best = new Choice { Velocity = _preferred, Cost = penalty + Straying(_preferred) };
            Try(ref best, float2.zero, StandingExtra());
            if (_start.Handling.IsInstant)
            {
                Try(ref best, _last, 0f);
                TryDirections(ref best);
            }
            else
            {
                TryReachable(ref best);
            }

            return best.Velocity;
        }

        // Clear of traffic, tracks reach a leg beyond their reachable headings only by stopping to turn, so that isn't
        // waiting. Among neighbours standing still blocks them, so it keeps its cost.
        private readonly float StandingExtra()
        {
            if (!_start.Chassis.Pivots || _crowd.Count > 0)
            {
                return StandingCost;
            }

            var reachable = _start.TurnWithin(_start.TopSpeed * 0.5f, ReachTime);
            return math.dot(_start.Facing, _leg) < math.cos(reachable) ? 0f : StandingCost;
        }

        // Stopped short of a wall it is facing, which only backing off and turning gets it away from.
        private readonly bool IsWedged(float2 chosen)
        {
            if (math.lengthsq(chosen) > Unchanged)
            {
                return false;
            }

            var forward = _start.Facing * _start.TopSpeed;
            var axis = _start.Facing * _halfSegment;
            return _walls.Depth(forward, axis, Soon, _walls.Resting(axis)) > 0f;
        }

        // Instant handling may take any direction at once, so the ring is turned to start on the planned one.
        private readonly void TryDirections(ref Choice best)
        {
            var forward = math.lengthsq(_preferred) > 1e-6f ? math.normalize(_preferred) : _start.Facing;
            for (var side = -2; side <= 2; side++)
            {
                Try(ref best, Planar.Rotate(_preferred, side * Nudge), 0f);
            }

            for (var i = 0; i < Directions; i++)
            {
                var direction = Planar.Rotate(forward, i * (math.PI * 2f / Directions));
                for (var speed = 1; speed <= 3; speed++)
                {
                    Try(ref best, direction * (_start.TopSpeed * speed / 3f), 0f);
                }
            }
        }

        private readonly void TryReachable(ref Choice best)
        {
            // The driver owns the gear. Avoidance only chooses reachable headings and slower speeds.
            var travel = _start.State.Reverse ? -_start.Facing : _start.Facing;
            for (var share = 1; share <= 4; share++)
            {
                var speed = _start.TopSpeed * share * 0.25f;
                var spread = _start.TurnWithin(speed, ReachTime);
                for (var i = 0; i < Headings; i++)
                {
                    var angle = spread * (i * 2f / (Headings - 1) - 1f);
                    Try(ref best, Planar.Rotate(travel, angle) * speed, 0f);
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
            var left = math.max(0f, Planar.Cross(math.normalizesafe(_preferred), candidate));
            var straying = math.distance(candidate, _preferred) + Stickiness * math.distance(candidate, _last) +
                           KeepRight * left;
            return straying / _start.TopSpeed;
        }

        /// <summary>Metres of overlap with neighbours and walls, weighted to count sooner hits more.</summary>
        private readonly float Penalty(float2 candidate)
        {
            var moving = _halfSegment > 0f && math.lengthsq(candidate) > 1e-6f;
            var axis = moving ? math.normalize(candidate) * _halfSegment : _crowd.Axis;
            var resting = _walls.Resting(axis);
            return PenaltyAt(candidate, axis, resting, 0f, Soon) + PenaltyAt(candidate, axis, resting, Soon, Mid) +
                   PenaltyAt(candidate, axis, resting, Mid, Horizon);
        }

        private readonly float PenaltyAt(float2 candidate, float2 axis, float resting, float from, float time)
        {
            var overlap = _crowd.Overlap(axis, candidate, from, time);
            return (overlap + _walls.Depth(candidate, axis, time, resting)) / time;
        }

        private struct Choice
        {
            public float2 Velocity;
            public float Cost;
        }
    }
}
