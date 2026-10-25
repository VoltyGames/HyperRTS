using HyperRTS.Simulation.Navigation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    /// <summary>
    /// One neighbour query per unit for both crowd responses. A mover gathers its nearest neighbours for ORCA local
    /// avoidance: <see cref="Avoid"/> picks the velocity closest to its planned one that keeps clear of them for
    /// <see cref="TimeHorizon"/>, and two movers share the avoiding. Every unit also sums the overlap push behind it:
    /// footprints are capsules (circles when the segment is zero) and must stay the larger of the two spacings apart,
    /// pushed along the line between their closest points, half each, as both move. A busy or enemy parked unit holds
    /// its ground against a mover, which avoids it alone and takes the whole push. An idle parked unit of the mover's
    /// side shares both and steps out of the mover's lane (<see cref="GivingWay"/>). Units on layers that never share
    /// a surface (a ship under a bridge, a tank on it) ignore each other.
    /// </summary>
    internal struct CrowdVisitor : ISpatialVisitor
    {
        public const int MaxNeighbours = 8;
        public const float TimeHorizon = 1.5f;

        // Squared change in velocity below which avoidance leaves the planned step alone.
        private const float SteerThreshold = 1e-4f;

        public Entity Self;
        public float2 Position;
        public float Radius;
        public float2 SegmentAxis;
        public float Spacing;
        public NavLayer Layer;
        public byte Faction;
        public bool Moving;
        public float2 Goal;
        public int Group;
        public GivingWay Rule;
        public float2 Push;

        /// <summary>A mover touched an arrived groupmate lying toward its goal, so it has arrived too.</summary>
        public bool ReachedGroup;

        private FixedList512Bytes<AvoidanceNeighbour> _neighbours;

        public CrowdVisitor(Entity self, float2 position, float2 axis, in NavAgent agent, byte faction, bool moving,
            in MoveDestination destination, in GivingWay rule)
        {
            Self = self;
            Position = position;
            Radius = agent.Radius;
            SegmentAxis = axis;
            Spacing = agent.Spacing;
            Layer = agent.Layer;
            Faction = faction;
            Moving = moving;
            Goal = destination.Value.xz;
            Group = destination.Group;
            Rule = rule;
            Push = float2.zero;
            ReachedGroup = false;
            _neighbours = default;
        }

        /// <summary>Where to query round; queries only read XZ.</summary>
        public readonly float3 Center => new(Position.x, 0f, Position.y);

        /// <summary>A mover looks as far as two units closing at top speed cover within the time horizon.</summary>
        public readonly float Reach(float maxSpeed)
        {
            var overlap = Radius + math.length(SegmentAxis) + Spacing;
            return Moving ? overlap + maxSpeed * TimeHorizon * 2f : overlap;
        }

        /// <summary>The velocity nearest <paramref name="planned"/> that keeps clear of the gathered neighbours.</summary>
        public readonly float2 Avoid(float2 velocity, float2 planned, float maxSpeed, float deltaTime)
        {
            var planes = new VelocityPlanes();
            foreach (var neighbour in _neighbours)
            {
                planes.Add(neighbour.Plane(velocity, TimeHorizon, deltaTime));
            }

            return planes.Solve(planned, maxSpeed);
        }

        /// <summary>
        /// Drives the leg, steering round the gathered neighbours when that changes the step. A three-point turn
        /// manoeuvres at a crawl, and steering would break its legs.
        /// </summary>
        public readonly float2 Drive(in Locomotion locomotion, ref LocomotionState drive, float2 facing,
            ref float2 heading, in PathLeg leg, float2 velocity, float topSpeed, float deltaTime)
        {
            var start = drive;
            heading = facing;
            var step = locomotion.Drive(ref drive, Position, ref heading, leg, topSpeed, Radius, deltaTime);
            if (drive.InThreePointTurn)
            {
                return step;
            }

            var planned = step / deltaTime;
            var chosen = Avoid(velocity, planned, topSpeed, deltaTime);
            if (math.distancesq(chosen, planned) <= SteerThreshold)
            {
                return step;
            }

            drive = start;
            heading = facing;
            return locomotion.Steer(ref drive, Position, ref heading, leg, chosen, topSpeed, Radius, deltaTime);
        }

        /// <summary>The overlap push, no longer than one frame's step.</summary>
        public readonly float2 PushWithin(float maxStep)
        {
            var length = math.length(Push);
            return length > maxStep ? Push * (maxStep / length) : Push;
        }

        public void Visit(in SpatialEntry entry)
        {
            if (!entry.IsUnit || entry.Entity == Self || !NavLayers.CanBlock(Layer, entry.Layer))
            {
                return;
            }

            var offset = entry.ClosestOffset(Position, SegmentAxis);
            var distanceSq = math.lengthsq(offset);
            var reach = Radius + entry.Radius + math.max(Spacing, entry.Spacing);
            if (Moving)
            {
                Gather(entry, -offset, distanceSq, reach);
            }

            if (distanceSq < reach * reach)
            {
                Separate(entry, offset, math.sqrt(distanceSq), reach);
            }
        }

        private void Gather(in SpatialEntry entry, float2 offset, float distanceSq, float reach)
        {
            // Stacked units have no line between them; the overlap push parts them first.
            if (IsWelcome(entry) || distanceSq < 1e-8f)
            {
                return;
            }

            Keep(new AvoidanceNeighbour
            {
                Offset = offset,
                Velocity = entry.Velocity,
                Reach = reach,
                Share = Share(entry),
                DistanceSq = distanceSq,
            });
        }

        private void Separate(in SpatialEntry entry, float2 offset, float distance, float reach)
        {
            var direction = Direction(offset, distance, entry);
            var overlap = reach - distance;
            ReachedGroup |= Moving && IsArrivedGroupmate(entry) &&
                            math.dot(entry.Position.xz - Position, Goal - Position) > 0f;
            if (entry.IsMoving == Moving)
            {
                Push += direction * (overlap * 0.5f);
            }
            else if (Moving)
            {
                // Only the mover gives way to a busy or enemy unit, so a truck can't shove a dozer that is building.
                Push += direction * (overlap * Share(entry));
            }
            else if (Rule.Yields(Self, Faction, entry.Faction))
            {
                Push += StepAside(direction, entry) * (overlap * 0.5f);
            }
        }

        // A mover arrives by touching an arrived groupmate and waits against a unit on its goal, so it avoids neither.
        private readonly bool IsWelcome(in SpatialEntry entry) =>
            IsArrivedGroupmate(entry) ||
            (!entry.IsMoving && math.distance(Goal, entry.Position.xz) < entry.HullReach + Radius);

        private readonly bool IsArrivedGroupmate(in SpatialEntry entry) =>
            Group != 0 && entry.Group == Group && entry.IsArrived && !entry.IsMoving;

        /// <summary>The mover's part: half against a unit that gives way too, all against one holding its ground.</summary>
        private readonly float Share(in SpatialEntry entry)
        {
            if (entry.IsMoving || Rule.Yields(entry.Entity, entry.Faction, Faction))
            {
                return 0.5f;
            }

            return 1f;
        }

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

        /// <summary>Away from the mover and out of its lane, toward its left head-on, as it keeps right.</summary>
        private readonly float2 StepAside(float2 direction, in SpatialEntry mover)
        {
            var left = new float2(-mover.Facing.y, mover.Facing.x);
            var side = math.dot(Position - mover.Position.xz, left) < 0f ? -left : left;
            return math.normalizesafe(direction + side);
        }

        // Crossed segments have no closest-point line, so fall back to the centres, then to a tie-break.
        private readonly float2 Direction(float2 offset, float distance, in SpatialEntry entry)
        {
            if (distance > 1e-4f)
            {
                return offset / distance;
            }

            var centres = Position - entry.Position.xz;
            var apart = math.length(centres);
            return apart > 1e-4f ? centres / apart : TieBreak(entry.Entity);
        }

        // Stacked units need opposite, deterministic directions: hash the pair, flip by index order.
        private readonly float2 TieBreak(Entity other)
        {
            var pair = new int2(math.min(Self.Index, other.Index), math.max(Self.Index, other.Index));
            math.sincos(math.hash(pair) * (math.PI * 2f / uint.MaxValue), out var sin, out var cos);
            return new float2(cos, sin) * (Self.Index < other.Index ? 1f : -1f);
        }
    }
}
