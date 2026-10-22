using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    /// <summary>
    /// Sums how far a unit must move to stop crowding neighbouring units (half each, as both move). Footprints are
    /// capsules (circles when the segment is zero) and must stay the larger of the two spacings apart, pushed along
    /// the line between their closest points. A busy or enemy parked unit holds its ground
    /// against a mover, which takes the whole push and steers round it unless it stands on the goal. An idle parked
    /// unit of the mover's side shares the push and steps out of the mover's lane (<see cref="GivingWay"/>). Units
    /// on layers that never share a surface (a ship under a bridge, a tank on it) ignore each other.
    /// </summary>
    internal struct SeparationVisitor : ISpatialVisitor
    {
        public Entity Self;
        public float2 Position;
        public float Radius;
        public float2 SegmentAxis;
        public float Spacing;
        public NavLayer Layer;
        public byte Faction;
        public bool Moving;
        public float2 Step;
        public float2 Goal;
        public int Group;
        public GivingWay Rule;
        public float2 Push;

        /// <summary>A mover touched an arrived groupmate lying toward its goal, so it has arrived too.</summary>
        public bool ReachedGroup;

        public SeparationVisitor(Entity self, float2 position, float2 axis, in NavAgent agent, byte faction,
            bool moving, float2 step, in MoveDestination destination, in GivingWay rule)
        {
            Self = self;
            Position = position;
            Radius = agent.Radius;
            SegmentAxis = axis;
            Spacing = agent.Spacing;
            Layer = agent.Layer;
            Faction = faction;
            Moving = moving;
            Step = step;
            Goal = destination.Value.xz;
            Group = destination.Group;
            Rule = rule;
            Push = float2.zero;
            ReachedGroup = false;
        }

        /// <summary>Where to query round; queries only read XZ.</summary>
        public readonly float3 Center => new(Position.x, 0f, Position.y);

        public readonly float Reach => Radius + math.length(SegmentAxis) + Spacing;

        public void Visit(in SpatialEntry entry)
        {
            if (!entry.IsUnit || entry.Entity == Self)
            {
                return;
            }

            if (!NavLayers.CanBlock(Layer, entry.Layer))
            {
                return;
            }

            var offset = ClosestOffset(entry.Position.xz, entry.SegmentAxis);
            var reach = Radius + entry.Radius + math.max(Spacing, entry.Spacing);
            var distanceSq = math.lengthsq(offset);
            if (distanceSq >= reach * reach)
            {
                return;
            }

            var distance = math.sqrt(distanceSq);
            var direction = Direction(offset, distance, entry);
            var overlap = reach - distance;
            ReachedGroup |= Moving && IsArrivedGroupmate(entry);
            if (entry.IsMoving == Moving)
            {
                Push += direction * (overlap * 0.5f);
            }
            else if (Moving)
            {
                PushPast(direction, overlap, entry);
            }
            else if (Rule.Yields(Self, Faction, entry.Faction))
            {
                Push += StepAside(direction, entry) * (overlap * 0.5f);
            }
        }

        // Only the mover gives way to a busy or enemy unit, so a truck can't shove a dozer that is building.
        private void PushPast(float2 direction, float overlap, in SpatialEntry entry)
        {
            if (Rule.Yields(entry.Entity, entry.Faction, Faction))
            {
                Push += direction * (overlap * 0.5f);
                return;
            }

            Push += direction * overlap + Deflection(direction, entry);
        }

        /// <summary>Away from the mover and out of its lane, toward its left head-on, as it keeps right.</summary>
        private readonly float2 StepAside(float2 direction, in SpatialEntry mover)
        {
            var left = new float2(-mover.Facing.y, mover.Facing.x);
            var side = math.dot(Position - mover.Position.xz, left) < 0f ? -left : left;
            return math.normalizesafe(direction + side);
        }

        private readonly bool IsArrivedGroupmate(in SpatialEntry entry)
        {
            if (Group == 0 || entry.Group != Group || !entry.IsArrived || entry.IsMoving)
            {
                return false;
            }

            return math.dot(entry.Position.xz - Position, Goal - Position) > 0f;
        }

        /// <summary>Turns the part of the step that runs into a parked unit along its side, keeping right head-on.</summary>
        private readonly float2 Deflection(float2 direction, in SpatialEntry entry)
        {
            var into = -math.dot(Step, direction);
            if (into <= 0f || IsGoalTaken(entry))
            {
                return float2.zero;
            }

            var side = new float2(-direction.y, direction.x);
            var lean = math.dot(side, Step);
            if (math.abs(lean) < 1e-6f)
            {
                lean = math.dot(side, new float2(Step.y, -Step.x));
            }

            return side * (math.sign(lean) * into);
        }

        // Going round a unit parked on the goal would circle it forever, so the mover waits against it instead.
        private readonly bool IsGoalTaken(in SpatialEntry entry) =>
            math.distance(Goal, entry.Position.xz) < entry.HullReach + Radius;

        /// <summary>From the other unit's segment to this one's, between their closest points.</summary>
        private readonly float2 ClosestOffset(float2 other, float2 otherAxis)
        {
            // Closest points of segments P(s) and Q(t), s and t in -1..1 (Ericson, Real-Time Collision Detection 5.1.9).
            var r = Position - other;
            var a = math.lengthsq(SegmentAxis);
            var e = math.lengthsq(otherAxis);
            var b = math.dot(SegmentAxis, otherAxis);
            var c = math.dot(SegmentAxis, r);
            var f = math.dot(otherAxis, r);
            var denominator = a * e - b * b;
            var s = denominator > 1e-6f ? math.clamp((b * f - c * e) / denominator, -1f, 1f) : 0f;
            var t = e > 1e-6f ? (b * s + f) / e : 0f;
            if (t < -1f || t > 1f)
            {
                t = math.clamp(t, -1f, 1f);
                s = a > 1e-6f ? math.clamp((b * t - c) / a, -1f, 1f) : 0f;
            }
            else if (a > 1e-6f && e <= 1e-6f)
            {
                s = math.clamp(-c / a, -1f, 1f);
            }

            return r + SegmentAxis * s - otherAxis * t;
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
