using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    /// <summary>
    /// One neighbour query per unit, gathering the neighbours <see cref="Steering"/> avoids and summing the push that
    /// keeps footprints apart.
    /// </summary>
    internal struct CrowdVisitor : ISpatialVisitor
    {
        public const int MaxNeighbours = 8;

        // Goals within 60 degrees of each other share a heading, so a crossing pair still shares the avoiding.
        private const float SharedHeading = 0.5f;

        public Entity Self;
        public float2 Position;
        public float Radius;
        public float2 SegmentAxis;
        /// <summary>Unit direction to its goal while it moves, else zero.</summary>
        public float2 Heading;
        public float Spacing;
        public NavLayer Layer;
        public byte Faction;
        public bool Moving;
        public float2 Goal;
        public int Group;

        /// <summary>Within the formation's <see cref="MoveDestination.Extent"/> of its own slot.</summary>
        public bool InGoalArea;
        public GivingWay Rule;
        public float2 Push;

        /// <summary>A mover touched an arrived groupmate lying toward its goal, so it has arrived too.</summary>
        public bool ReachedGroup;

        private FixedList512Bytes<AvoidanceNeighbour> _neighbours;
        private HullRoom _room;

        public CrowdVisitor(Entity self, float2 position, float2 facing, in NavAgent agent, byte faction, bool moving,
            in MoveDestination destination, in GivingWay rule)
        {
            Self = self;
            Position = position;
            Radius = agent.Radius;
            SegmentAxis = facing * agent.HalfSegment;
            Spacing = agent.Spacing;
            Layer = agent.Layer;
            Faction = faction;
            Moving = moving;
            Goal = destination.Value.xz;
            Heading = moving ? math.normalizesafe(Goal - position) : float2.zero;
            Group = destination.Group;
            InGoalArea = math.distance(position, Goal) <= destination.Extent;
            Rule = rule;
            Push = float2.zero;
            ReachedGroup = false;
            _neighbours = default;
            _room = default;
        }

        /// <summary>Where to query round; queries only read XZ.</summary>
        public readonly float3 Center => new(Position.x, 0f, Position.y);

        /// <summary>A mover looks as far as two units closing at top speed cover within the steering horizon.</summary>
        public readonly float Reach(float maxSpeed)
        {
            var overlap = Radius + math.length(SegmentAxis) + Spacing;
            return Moving ? overlap + maxSpeed * Steering.Horizon * 2f : overlap;
        }

        /// <summary>The nearest neighbours a mover steers round.</summary>
        public readonly FixedList512Bytes<AvoidanceNeighbour> Neighbours => _neighbours;

        public readonly bool HasNeighbours => _neighbours.Length > 0;

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
            var distance = math.sqrt(distanceSq);
            var reach = Radius + entry.Radius + math.max(Spacing, entry.Spacing);
            var precedence = Precedence(entry);
            if (Moving)
            {
                Gather(entry, distanceSq, distance, reach, precedence);
            }

            // Beyond twice the spacing no pair closes the room left in one frame, so it needn't count.
            var watched = reach + math.max(Spacing, entry.Spacing);
            if (distanceSq >= watched * watched)
            {
                return;
            }

            var direction = Direction(offset, distance, entry);
            // Parked pairs only part, and a mover may press an idle ally aside, so neither pair limits the move.
            if (Moving ? !IsMakingWay(entry) : entry.IsMoving)
            {
                _room.Add(direction, distance - Radius - entry.Radius);
            }

            if (distance < reach)
            {
                Separate(entry, direction, distance, reach, precedence);
            }
        }

        /// <summary>The move cut so no neighbour's hull is met; see <see cref="HullRoom"/>.</summary>
        public readonly float2 Within(float2 move) => _room.Within(move);

        /// <summary>Whether the move meets no neighbour's hull.</summary>
        public readonly bool Keeps(float2 move) => _room.Keeps(move);

        private void Gather(in SpatialEntry entry, float distanceSq, float distance, float reach, int precedence)
        {
            // Stacked units have no line between them; the overlap push parts them first.
            if (IsWelcome(entry) || IsMakingWay(entry) || distanceSq < 1e-8f)
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
                Share = precedence == 0 ? Share(entry) : 1f,
                DistanceSq = distanceSq,
                Contact = precedence < 0 ? 0f : math.max(0f, reach - distance),
            });
        }

        private void Separate(in SpatialEntry entry, float2 direction, float distance, float reach, int precedence)
        {
            var overlap = reach - distance;
            ReachedGroup |= Moving && IsArrivedGroupmate(entry) &&
                            math.dot(entry.Position.xz - Position, Goal - Position) > 0f;
            if (Moving)
            {
                Push += direction * MoverPush(entry, distance, reach, precedence);
            }
            else if (!entry.IsMoving)
            {
                Push += direction * (overlap * 0.5f);
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

        // Steering round an idle ally would stop the mover short in a gap; it presses on and the push moves the ally.
        private readonly bool IsMakingWay(in SpatialEntry entry) =>
            !entry.IsMoving && Rule.Yields(entry.Entity, entry.Faction, Faction);

        // Only in the goal area, or a groupmate parked on the route would end the move there.
        private readonly bool IsArrivedGroupmate(in SpatialEntry entry) =>
            InGoalArea && Group != 0 && entry.Group == Group && entry.IsArrived && !entry.IsMoving;

        /// <summary>
        /// The mover's part: half against a unit that gives way too, all against one holding its ground, so a truck
        /// can't shove a dozer that is building.
        /// </summary>
        private readonly float Share(in SpatialEntry entry)
        {
            if (entry.IsMoving || Rule.Yields(entry.Entity, entry.Faction, Faction))
            {
                return 0.5f;
            }

            return 1f;
        }

        // Same-way traffic makes its spacing by the follower alone, but inside the leader's own reach both part half
        // each, so a follower pressed from both sides can't be driven into.
        private readonly float MoverPush(in SpatialEntry entry, float distance, float reach, int precedence)
        {
            var overlap = reach - distance;
            if (precedence == 0)
            {
                return overlap * Share(entry);
            }

            var close = math.max(0f, LeaderReach(entry, reach) - distance) * 0.5f;
            return precedence > 0 ? close : overlap - close;
        }

        // Ahead of same-way traffic a mover keeps half the spacing, so it drives on while the follower backs off.
        private readonly float LeaderReach(in SpatialEntry entry, float reach) =>
            (reach + Radius + entry.Radius) * 0.5f;

        // Two movers going one way at a gap too narrow for both would each wait on the other, so the one further back
        // along their shared heading gives way alone. Head-on movers share the avoiding.
        private readonly int Precedence(in SpatialEntry entry)
        {
            if (!Moving || !entry.IsMoving || math.dot(Heading, entry.Heading) < SharedHeading)
            {
                return 0;
            }

            var ahead = math.dot(Position - entry.Position.xz, Heading + entry.Heading);
            if (ahead != 0f)
            {
                return ahead > 0f ? 1 : -1;
            }

            return Self.Index < entry.Entity.Index ? 1 : -1;
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
            var left = Planar.Left(mover.Facing);
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
