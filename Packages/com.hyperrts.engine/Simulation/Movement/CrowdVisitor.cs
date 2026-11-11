using Unity.Collections;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Spatial;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Movement
{
    /// <summary>
    /// One neighbour query per unit, gathering the neighbours <see cref="Steering"/> avoids and summing the push that
    /// keeps footprints apart. <see cref="RightOfWay"/> says who moves.
    /// </summary>
    internal struct CrowdVisitor : ISpatialVisitor
    {
        /// <summary>The unit itself, as its neighbours see it in the index.</summary>
        public SpatialEntry Unit;
        public float2 Goal;

        /// <summary>Within the formation's <see cref="MoveDestination.Extent"/> of its own slot.</summary>
        public bool InGoalArea;
        public RightOfWay Rule;
        public float2 Push;

        /// <summary>A mover touched an arrived groupmate lying toward its goal, so it has arrived too.</summary>
        public bool ReachedGroup;

        /// <summary>A vehicle done waiting on allied foot units, which no longer hold it until it is clear of them.</summary>
        public bool PassesInfantry;

        /// <summary>An allied foot unit stands in this moving vehicle's lane, so it slows to a walk.</summary>
        public bool InfantryAhead;

        private AvoidanceList _neighbours;
        private HullClearance _room;
        private bool _infantryTouches;
        private bool _othersTouch;
        private bool _infantryNear;

        public CrowdVisitor(in SpatialEntry unit, in MoveDestination destination, in RightOfWay rule)
        {
            Unit = unit;
            Goal = destination.Value.xz;
            InGoalArea = math.distance(unit.Position.xz, Goal) <= destination.Extent;
            Rule = rule;
            Push = float2.zero;
            ReachedGroup = false;
            PassesInfantry = false;
            InfantryAhead = false;
            _neighbours = default;
            _room = default;
            _infantryTouches = false;
            _othersTouch = false;
            _infantryNear = false;
        }

        public readonly float2 Position => Unit.Position.xz;
        public readonly float Radius => Unit.Radius;

        /// <summary>Half the core segment along its facing; zero for round footprints.</summary>
        public readonly float2 SegmentAxis => Unit.SegmentAxis;

        private readonly bool Moving => Unit.IsMoving;

        /// <summary>Where to query round; queries only read XZ.</summary>
        public readonly float3 Center => Unit.Position;

        /// <summary>
        /// A mover looks as far as two units closing at top speed cover within the steering horizon, and a foot unit
        /// near a moving vehicle far enough to see its lane.
        /// </summary>
        public readonly float Reach(float maxSpeed, bool nearVehicleLane)
        {
            var sight = !Unit.IsVehicle && nearVehicleLane ? SpatialIndex.LaneSight : 0f;
            var closing = Moving ? maxSpeed * Steering.Horizon * 2f : 0f;
            return Unit.HullReach + math.max(closing, sight);
        }

        /// <summary>The nearest neighbours a mover steers round.</summary>
        public readonly FixedList512Bytes<AvoidanceNeighbour> Neighbours => _neighbours.Items;

        public readonly bool HasNeighbours => _neighbours.Count > 0;

        /// <summary>Allied foot units stand in its lane, or it is driving through them, so it keeps to a walk.</summary>
        public readonly bool WalksForInfantry => InfantryAhead || (PassesInfantry && _infantryNear);

        /// <summary>Allied foot units alone stop this vehicle: one touches it in its lane and nothing else does.</summary>
        public readonly bool IsHeldByInfantry => _infantryTouches && !_othersTouch;

        /// <summary>An allied foot unit is within this vehicle's spacing, so it is not yet clear of them.</summary>
        public readonly bool IsNearInfantry => _infantryNear;

        public void Visit(in SpatialEntry entry)
        {
            if (!entry.IsUnit || entry.Entity == Unit.Entity || !NavLayers.CanBlock(Unit.Layer, entry.Layer)
                || Rule.IsCrushing(Unit, entry))
            {
                return;
            }

            var offset = entry.ClosestOffset(Position, SegmentAxis);
            var distanceSq = math.lengthsq(offset);
            var distance = math.sqrt(distanceSq);
            var reach = Radius + entry.Radius + math.max(Unit.Spacing, entry.Spacing);
            var precedence = Rule.Precedence(Unit, entry);
            var entryYields = Rule.Yields(entry, Unit);
            WatchLane(entry, distance < reach, entryYields);
            if (Moving)
            {
                Gather(entry, distanceSq, distance, reach, precedence, entryYields);
            }

            // Beyond twice the spacing no pair closes the room left in one frame, so it needn't count.
            var watched = reach + math.max(Unit.Spacing, entry.Spacing);
            if (distanceSq >= watched * watched)
            {
                return;
            }

            _infantryNear |= Unit.IsVehicle && !entry.IsVehicle && entryYields;
            var direction = Unit.DirectionAway(offset, distance, entry);
            // Parked pairs only part, and a mover presses aside a unit giving way to it, so neither limits the move.
            if (Moving ? !Presses(entry, entryYields) : entry.IsMoving)
            {
                _room.Add(direction, distance - Radius - entry.Radius);
            }

            if (distance < reach)
            {
                Separate(entry, direction, distance, reach, precedence, entryYields);
            }
        }

        /// <summary>The move cut so no neighbour's hull is met; see <see cref="HullClearance"/>.</summary>
        public readonly float2 Within(float2 move) => _room.Within(move);

        /// <summary>Whether the move meets no neighbour's hull.</summary>
        public readonly bool Keeps(float2 move) => _room.Keeps(move);

        private void Gather(in SpatialEntry entry, float distanceSq, float distance, float reach, int precedence,
            bool entryYields)
        {
            // Stacked units have no line between them; the overlap push parts them first.
            if (IsWelcome(entry) || distanceSq < 1e-8f)
            {
                return;
            }

            var share = Rule.Share(Unit, entry, entryYields, precedence);
            if (share <= 0f)
            {
                return;
            }

            // Behind same-way traffic a mover counts the whole spacing it already lacks, so it backs off.
            reach = precedence > 0 ? LeaderReach(entry, reach) : reach;
            _neighbours.Keep(new AvoidanceNeighbour
            {
                Position = entry.Position.xz,
                SegmentAxis = entry.SegmentAxis,
                Velocity = entry.Velocity,
                Reach = reach,
                Share = share,
                DistanceSq = distanceSq,
                Contact = precedence < 0 ? 0f : math.max(0f, reach - distance),
            });
        }

        // A foot unit steps out of an allied vehicle's lane; a vehicle notes who stands in its own.
        private void WatchLane(in SpatialEntry entry, bool touching, bool entryYields)
        {
            if (!Unit.IsVehicle)
            {
                if (entry.IsVehicle && entry.IsMoving && Rule.Yields(Unit, entry))
                {
                    Push += Rule.StepAside(entry, Unit);
                }

                return;
            }

            if (!Moving || !Rule.IsInLane(Unit, entry))
            {
                return;
            }

            var infantry = !entry.IsVehicle && entryYields;
            InfantryAhead |= infantry;
            _infantryTouches |= infantry && touching;
            _othersTouch |= !infantry && touching;
        }

        // A mover presses aside a parked unit giving way, as the push moves it; a vehicle presses foot units only once
        // done waiting on them.
        private readonly bool Presses(in SpatialEntry entry, bool entryYields)
        {
            if (!entryYields)
            {
                return false;
            }

            return Unit.IsVehicle && !entry.IsVehicle ? PassesInfantry : !entry.IsMoving;
        }

        // A mover arrives by touching an arrived groupmate and waits against a unit on its goal, so it avoids neither.
        private readonly bool IsWelcome(in SpatialEntry entry) =>
            IsArrivedGroupmate(entry) ||
            (!entry.IsMoving && math.distance(Goal, entry.Position.xz) < entry.HullReach + Radius);

        // Only in the goal area, or a groupmate parked on the route would end the move there.
        private readonly bool IsArrivedGroupmate(in SpatialEntry entry) =>
            InGoalArea && Unit.Group != 0 && entry.Group == Unit.Group && entry.IsArrived && !entry.IsMoving;

        /// <summary>The overlap push, no longer than one frame's step.</summary>
        public readonly float2 PushWithin(float maxStep)
        {
            var length = math.length(Push);
            return length > maxStep ? Push * (maxStep / length) : Push;
        }

        private void Separate(in SpatialEntry entry, float2 direction, float distance, float reach, int precedence,
            bool entryYields)
        {
            var overlap = reach - distance;
            ReachedGroup |= Moving && IsArrivedGroupmate(entry) &&
                            math.dot(entry.Position.xz - Position, Goal - Position) > 0f;
            // The push never moves a vehicle off a foot unit, so the foot unit takes the whole overlap.
            if (Unit.IsVehicle != entry.IsVehicle)
            {
                Push += direction * (overlap * Rule.PushShare(Unit, entry, entryYields));
            }
            else if (Moving)
            {
                Push += direction * MoverPush(entry, distance, reach, precedence, entryYields);
            }
            else if (!entry.IsMoving)
            {
                Push += direction * (overlap * 0.5f);
            }
            else if (Rule.Yields(Unit, entry))
            {
                var aside = RightOfWay.Side(entry.Facing, Position - entry.Position.xz);
                Push += math.normalizesafe(direction + aside) * (overlap * 0.5f);
            }
        }

        // Same-way traffic makes its spacing by the follower alone, but inside the leader's own reach both part half
        // each, so a follower pressed from both sides can't be driven into.
        private readonly float MoverPush(in SpatialEntry entry, float distance, float reach, int precedence,
            bool entryYields)
        {
            var overlap = reach - distance;
            if (precedence == 0)
            {
                return overlap * Rule.PushShare(Unit, entry, entryYields);
            }

            var close = math.max(0f, LeaderReach(entry, reach) - distance) * 0.5f;
            return precedence > 0 ? close : overlap - close;
        }

        // Ahead of same-way traffic a mover keeps half the spacing, so it drives on while the follower backs off.
        private readonly float LeaderReach(in SpatialEntry entry, float reach) =>
            (reach + Radius + entry.Radius) * 0.5f;
    }
}
