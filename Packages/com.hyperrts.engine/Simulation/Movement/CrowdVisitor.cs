using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Spatial;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Movement
{
    /// <summary>
    /// One neighbour query per unit, gathering the neighbours <see cref="Steering"/> avoids and summing the push that
    /// keeps footprints apart. <see cref="RightOfWay"/> says who moves.
    /// </summary>
    internal partial struct CrowdVisitor : ISpatialVisitor
    {
        public const int MaxNeighbours = 8;

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

        private FixedList512Bytes<AvoidanceNeighbour> _neighbours;
        private HullClearance _room;
        private bool _infantryTouches;
        private bool _othersTouch;
        private bool _infantryNear;

        public CrowdVisitor(Entity self, float2 position, float2 facing, float2 velocity, in NavAgent agent,
            byte owner, bool moving, in MoveDestination destination, in RightOfWay rule)
        {
            Goal = destination.Value.xz;
            Unit = new SpatialEntry
            {
                Entity = self,
                Position = new float3(position.x, 0f, position.y),
                Radius = agent.Radius,
                Owner = owner,
                IsUnit = true,
                IsMoving = moving,
                Group = destination.Group,
                Facing = facing,
                Heading = moving ? math.normalizesafe(Goal - position) : float2.zero,
                Velocity = velocity,
                Layer = agent.Layer,
                SegmentAxis = facing * agent.HalfSegment,
                Spacing = agent.Spacing,
                Mass = agent.Mass,
                IsVehicle = agent.IsVehicle,
            };
            Unit.FixReach();
            InGoalArea = math.distance(position, Goal) <= destination.Extent;
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
        public readonly FixedList512Bytes<AvoidanceNeighbour> Neighbours => _neighbours;

        public readonly bool HasNeighbours => _neighbours.Length > 0;

        /// <summary>Allied foot units stand in its lane, or it is driving through them, so it keeps to a walk.</summary>
        public readonly bool WalksForInfantry => InfantryAhead || (PassesInfantry && _infantryNear);

        /// <summary>Allied foot units alone stop this vehicle: one touches it in its lane and nothing else does.</summary>
        public readonly bool IsHeldByInfantry => _infantryTouches && !_othersTouch;

        /// <summary>An allied foot unit is within this vehicle's spacing, so it is not yet clear of them.</summary>
        public readonly bool IsNearInfantry => _infantryNear;

        public void Visit(in SpatialEntry entry)
        {
            if (!entry.IsUnit || entry.Entity == Unit.Entity || !NavLayers.CanBlock(Unit.Layer, entry.Layer))
            {
                return;
            }

            if (Rule.IsCrushing(Unit, entry))
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
            var direction = Direction(offset, distance, entry);
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
            Keep(new AvoidanceNeighbour
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

        private void Keep(in AvoidanceNeighbour neighbour)
        {
            if (_neighbours.Length < MaxNeighbours)
            {
                _neighbours.Add(neighbour);
                return;
            }

            var farthest = 0;
            for (var i = 1; i < _neighbours.Length; i++)
            {
                farthest = _neighbours[i].DistanceSq > _neighbours[farthest].DistanceSq ? i : farthest;
            }

            if (neighbour.DistanceSq < _neighbours[farthest].DistanceSq)
            {
                _neighbours[farthest] = neighbour;
            }
        }
    }
}
