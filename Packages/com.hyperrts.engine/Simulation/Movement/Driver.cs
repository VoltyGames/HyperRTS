using Unity.Entities;
using HyperRTS.Simulation.Common.Geometry;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Movement
{
    /// <summary>
    /// Drives one vehicle for one frame with its <see cref="Locomotion"/>, turning <see cref="Facing"/> and returning
    /// the hull centre's move.
    /// </summary>
    public struct Driver
    {
        public Locomotion Handling;
        public Chassis Chassis;
        public float Radius;

        /// <summary>Metres per second forwards at most; never zero, so times can divide by it.</summary>
        public float TopSpeed;

        public float DeltaTime;

        /// <summary>The hull centre on XZ.</summary>
        public float2 Position;

        public float2 Facing;
        public LocomotionState State;

        private const float PivotAngle = math.PI / 3f;
        private const float Crawl = 0.5f;
        private const float LookAheadTime = 0.5f;
        private const float ReverseMargin = 1.3f;

        // Wheels turn round to a corner this far behind by backing, until it is this far round to the front.
        private static readonly float Behind = math.radians(100f);
        private static readonly float TurnedRound = math.radians(60f);

        public Driver(in Locomotion handling, in NavAgent agent, float topSpeed, float deltaTime)
        {
            Handling = handling;
            Chassis = agent.Chassis;
            Radius = agent.Radius;
            TopSpeed = math.max(topSpeed, 1e-3f);
            DeltaTime = deltaTime;
            Position = float2.zero;
            Facing = float2.zero;
            State = default;
        }

        /// <summary>How far ahead it may cut to a corner in sight: further when fast or wide-turning.</summary>
        public readonly float LookAhead =>
            Radius * 2f + math.abs(State.Speed) * LookAheadTime + Chassis.TurnRadius * 0.5f;

        /// <summary>Whether it stands to turn from <paramref name="facing"/> toward the leg rather than drive on.</summary>
        public readonly bool IsPivoting(float2 facing, in PathLeg leg) =>
            State.OtherWay <= 0f && IsPivot(Planar.SignedAngle(facing, State.Reverse ? -leg.Direction : leg.Direction));

        private readonly bool IsPivot(float error) => Chassis.Pivots && math.abs(error) > PivotAngle;

        /// <summary>Accelerates and turns toward the leg, returning this frame's move of the hull centre.</summary>
        public float2 Drive(in PathLeg leg) => Follow(leg, TopSpeed, float2.zero);

        /// <summary>
        /// Applies avoidance's heading and speed without changing the route's gear or recovery manoeuvre.
        /// </summary>
        public float2 Steer(in PathLeg leg, float2 velocity)
        {
            return Follow(leg, math.length(velocity), velocity);
        }

        private float2 Follow(in PathLeg leg, float cap, float2 velocity)
        {
            State.Reverse = leg.Reverse || PrefersReverse(leg);
            var lookAhead = LookAhead;
            var steering = math.lengthsq(velocity) > 1e-6f ? leg.Toward(Position, velocity) : leg;
            var aim = ToTarget(steering, lookAhead);
            // Tracks backing out of a wedge straighten with their tail toward the path, not their nose.
            var backsOut = Chassis.Pivots && State.OtherWay > 0f;
            var desired = math.normalizesafe(State.Reverse != backsOut ? -aim : aim, Facing);
            var error = Planar.SignedAngle(Facing, desired);
            // A route backed whole has no three-point turn: the goal behind is where it backs to.
            State.InThreePointTurn = !leg.Reverse && TurnsOtherWay(leg, error);
            var gear = State.Reverse == State.StepOtherWay(Chassis.IsWheeled, DeltaTime) ? 1f : -1f;
            var top = math.min(gear > 0f ? TopSpeed : Handling.ReverseSpeed, cap);
            // Pivoting is what a wedge blocks, so tracks back straight out of one, whatever the path's turns.
            // Wheels hold the arc to their aim, tracks the one to the corner.
            var arc = Chassis.IsWheeled ? math.length(aim) : leg.Distance;
            var cruise = backsOut ? top : math.min(top, TargetSpeed(error, leg, lookAhead, arc));
            // Never past the goal in one frame, which only bites for near-instant braking.
            var target = math.min(cruise, leg.Remaining / DeltaTime);
            State.Speed = Handling.Accelerate(State.Speed, gear * target, DeltaTime);
            // Steers for the way it still rolls, so braking into a gear change keeps swinging the nose round.
            var rolls = math.abs(State.Speed) > 1e-3f ? math.sign(State.Speed) : gear;
            return Chassis.IsWheeled ? Roll(rolls * Chassis.Pursuit(error, math.length(aim))) : TurnTracks(error);
        }

        // A corner inside the turning circle lies on no forward arc, and one behind takes a ring to reach forwards,
        // so wheels drive the other way to swing onto it. Each holds a little past its edge, so the gear doesn't
        // flip every frame.
        private readonly bool TurnsOtherWay(in PathLeg leg, float error)
        {
            if (!Chassis.IsWheeled)
            {
                return false;
            }

            if (Chassis.IsInsideTurn(Position, Facing, leg.Corner, State.InThreePointTurn ? 1.2f : 1f))
            {
                return true;
            }

            var reverses = Chassis.ReverseRun > 0f && Handling.ReverseSpeed > 0f;
            return reverses && math.abs(error) > (State.InThreePointTurn ? TurnedRound : Behind);
        }

        // Tracks turn about the centre at their turn rate and drive along the facing.
        private float2 TurnTracks(float error)
        {
            var limit = Handling.TurnRate * DeltaTime;
            Facing = Chassis.Rotate(Chassis.Pose(Position, Facing), math.clamp(error, -limit, limit)).Facing;
            return Facing * (State.Speed * DeltaTime);
        }

        /// <summary>The standing hull's facing turned toward <paramref name="direction"/> at its turn rate, as when aiming.</summary>
        public readonly float2 TurnToward(float2 direction) =>
            Planar.TurnToward(Facing, direction, Handling.TurnRate * DeltaTime);

        /// <summary>
        /// A parked unit with no other turn turns its hull to a hull-fixed weapon's target at its turn rate. The aim
        /// lasts one frame, so it is cleared here.
        /// </summary>
        public readonly float2 Aim(float2 heading, bool parked, in FacingGoal aim, EnabledRefRW<FacingGoal> aiming)
        {
            var turns = parked && aiming.ValueRO && math.all(heading == 0f);
            aiming.ValueRW = false;
            return turns ? TurnToward(aim.Direction) : heading;
        }

        /// <summary>Brakes to a stop along its facing once it has nowhere to go.</summary>
        public float2 BrakeToStop()
        {
            State.Speed = Handling.Accelerate(State.Speed, 0f, DeltaTime);
            State.Reverse = false;
            State.InThreePointTurn = false;
            State.OtherWay = 0f;
            State.LastChoice = float2.zero;
            return Facing * (State.Speed * DeltaTime);
        }

        public readonly bool HasArrived(in PathLeg leg)
        {
            if (leg.HasArrived(Radius))
            {
                return true;
            }

            // A goal under the hull but inside the turning circle would take a three-point turn to reach; backing,
            // it drives straight on.
            var underHull = leg.IsFinal && leg.Distance <= Radius && !leg.Reverse;
            return underHull && Chassis.IsInsideTurn(Position, Facing, leg.Corner, 1f);
        }

        // The speed the leg's turns and its end allow.
        private readonly float TargetSpeed(float error, in PathLeg leg, float lookAhead, float arc)
        {
            if (IsPivot(error))
            {
                return 0f;
            }

            var tolerance = NavTolerances.Arrive(Radius);
            var stop = math.max(Crawl, math.sqrt(2f * Handling.Braking * math.max(0f, leg.Remaining - tolerance)));
            var beforeCorner = math.max(0f, leg.Distance - lookAhead);
            var cornerSpeed = Handling.CornerSpeed(Chassis.TurnRadius, leg.CornerTurn, lookAhead);
            var corner = math.sqrt(math.square(cornerSpeed) + 2f * Handling.Braking * beforeCorner);
            var turn = Handling.TurnSpeed(Chassis.TurnRadius, arc, error);
            return math.min(turn, math.min(stop, corner));
        }

        /// <summary>Wheels can't turn standing, so a wheeled hull wedged against a wall backs off it a while.</summary>
        public void BackOff() => State.BackOff(Chassis.IsWheeled);

        /// <summary>Tracks that can't pivot in a slot too narrow for it back straight out until they can.</summary>
        public void BackOutOfPivot()
        {
            if (Chassis.Pivots && State.OtherWay <= 0f && Handling.CanTurnOut)
            {
                Handling.TurnOut(ref State);
            }
        }

        /// <summary>Radians the hull can turn within <paramref name="seconds"/> at <paramref name="speed"/>.</summary>
        public readonly float TurnWithin(float speed, float seconds) => Handling.TurnWithin(Chassis, speed, seconds);

        private readonly bool PrefersReverse(in PathLeg leg)
        {
            if (leg.Remaining > Chassis.ReverseRun)
            {
                return false;
            }

            // A turn round under way is finished, not swapped for backing as the goal comes into the reverse run.
            if (State.InThreePointTurn && !State.Reverse)
            {
                return false;
            }

            var along = math.dot(Facing, leg.Direction);
            if (along >= (State.Reverse ? 0.3f : -0.2f))
            {
                return false;
            }

            var backing = leg.Remaining / Handling.ReverseSpeed;
            // Tracks turn at their rate; wheels also need the arc of their turning circle rolled.
            var rolling = Chassis.Pivots ? 0f : Chassis.TurnRadius / TopSpeed;
            var turning = math.acos(math.max(-1f, along)) * math.max(1f / Handling.TurnRate, rolling);
            return turning + leg.Remaining / TopSpeed > backing * ReverseMargin;
        }

        // The rear axle aims at the point the look-ahead reaches along the path; tracks head along the leg.
        private readonly float2 ToTarget(in PathLeg leg, float lookAhead)
        {
            if (!Chassis.IsWheeled)
            {
                return leg.Direction;
            }

            return leg.PointAhead(Position, lookAhead) - Chassis.Pose(Position, Facing).Position;
        }

        /// <summary>Steers toward <paramref name="wanted"/> and rolls the rear axle along the arc.</summary>
        private float2 Roll(float wanted)
        {
            if (Chassis.IsWheeled)
            {
                State.Steer = Handling.SteerToward(Chassis, State.Steer, State.Speed, wanted, DeltaTime);
            }

            var pose = Chassis.Pose(Position, Facing).Advance(State.Steer, State.Speed * DeltaTime);
            Facing = math.normalize(pose.Facing);
            return Chassis.Center(pose) - Position;
        }
    }
}
