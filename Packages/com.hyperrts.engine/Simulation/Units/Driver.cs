using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    /// <summary>
    /// One vehicle driving one frame with its <see cref="Locomotion"/>: its hull, where it stands and the motion it
    /// carries between frames. Driving turns <see cref="Facing"/> and returns the hull centre's move.
    /// </summary>
    public partial struct Driver
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
        public float2 Drive(in PathLeg leg) => Follow(leg, TopSpeed);

        /// <summary>
        /// Drives toward the steering <paramref name="velocity"/> instead of the leg, no faster than it: the velocity
        /// becomes the curvature it steers for. A vehicle told to go back backs straight up if it can reverse, else
        /// brakes on the leg.
        /// </summary>
        public float2 Steer(in PathLeg leg, float2 velocity)
        {
            // Told to stand, it keeps turning toward the leg, as a pivot does.
            if (math.lengthsq(velocity) < 1e-6f)
            {
                return Follow(leg, 0f);
            }

            var travel = State.Reverse ? -Facing : Facing;
            if (!Handling.IsInstant && math.dot(velocity, travel) < 0f)
            {
                return GoBack(leg, math.length(velocity));
            }

            return Follow(leg.Toward(Position, velocity), math.length(velocity));
        }

        private float2 GoBack(in PathLeg leg, float speed)
        {
            if (State.Reverse || Handling.ReverseSpeed <= 0f)
            {
                return Follow(leg, 0f);
            }

            State.Speed = Handling.Accelerate(State.Speed, -math.min(speed, Handling.ReverseSpeed), DeltaTime);
            return Roll(0f);
        }

        private float2 Follow(in PathLeg leg, float cap)
        {
            if (leg.Tight)
            {
                return FollowTight(leg, cap);
            }

            State.Reverse = PrefersReverse(leg);
            // Keeps reversing a little past the circle's edge, so it doesn't flip gear every frame.
            State.InThreePointTurn =
                Chassis.IsInsideTurn(Position, Facing, leg.Corner, State.InThreePointTurn ? 1.2f : 1f);
            var lookAhead = LookAhead;
            var aim = ToTarget(leg, lookAhead);
            var desired = math.normalizesafe(State.Reverse ? -aim : aim, Facing);
            var error = Planar.SignedAngle(Facing, desired);
            var gear = State.Reverse == IsOtherWay() ? 1f : -1f;
            var top = math.min(gear > 0f ? TopSpeed : Handling.ReverseSpeed, cap);
            // Pivoting is what a wedge blocks, so tracks back out of one and turn as they go.
            var backsOut = Chassis.Pivots && State.OtherWay > 0f;
            // Never past the goal in one frame, which only bites for near-instant braking.
            var target = math.min(TargetSpeed(error, leg, top, lookAhead, backsOut), leg.Remaining / DeltaTime);
            State.Speed = Handling.Accelerate(State.Speed, gear * target, DeltaTime);
            return Chassis.IsWheeled ? Roll(gear * Pursuit(error, math.length(aim))) : TurnTracks(error);
        }

        // Tracks turn about the centre at their turn rate and drive along the facing.
        private float2 TurnTracks(float error)
        {
            var limit = Handling.TurnRate * DeltaTime;
            Facing = Chassis.Rotate(Chassis.Pose(Position, Facing), math.clamp(error, -limit, limit)).Facing;
            return Facing * (State.Speed * DeltaTime);
        }

        /// <summary>The standing hull's facing turned toward <paramref name="direction"/> at its turn rate, as when aiming.</summary>
        public readonly float2 TurnToward(float2 direction)
        {
            var limit = Handling.TurnRate * DeltaTime;
            return math.normalize(Planar.Rotate(Facing, math.clamp(Planar.SignedAngle(Facing, direction), -limit, limit)));
        }

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

            // A goal under the hull but inside the turning circle would take a three-point turn to reach.
            var underHull = leg.IsFinal && leg.Distance <= Radius;
            return underHull && Chassis.IsInsideTurn(Position, Facing, leg.Corner, 1f);
        }

        private readonly float TargetSpeed(float error, in PathLeg leg, float top, float lookAhead, bool backsOut)
        {
            // Backing out of a wedge runs straight off the walls, whatever the path's turns.
            if (backsOut)
            {
                return top;
            }

            if (IsPivot(error))
            {
                return 0f;
            }

            var tolerance = NavTolerances.Arrive(Radius);
            var stop = math.max(Crawl, math.sqrt(2f * Handling.Braking * math.max(0f, leg.Remaining - tolerance)));
            var beforeCorner = math.max(0f, leg.Distance - lookAhead);
            var cornerSpeed = Handling.CornerSpeed(Chassis.TurnRadius, leg.CornerTurn, lookAhead);
            var corner = math.sqrt(math.square(cornerSpeed) + 2f * Handling.Braking * beforeCorner);
            var turn = Handling.TurnSpeed(Chassis.TurnRadius, leg.Distance, error);
            return math.min(math.min(top, turn), math.min(stop, corner));
        }
    }
}
