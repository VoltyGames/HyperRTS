using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    /// <summary>
    /// Sums how far a unit must move to stop overlapping neighbouring units (half each, as both move), the fallback
    /// behind <see cref="AvoidanceVisitor"/>. Footprints are capsules (circles when the segment is zero) and must stay
    /// the larger of the two spacings apart, pushed along the line between their closest points. A busy or enemy
    /// parked unit holds its ground against a mover, which takes the whole push. An idle parked unit of the mover's
    /// side shares the push and steps out of the mover's lane (<see cref="GivingWay"/>). Units on layers that never
    /// share a surface (a ship under a bridge, a tank on it) ignore each other.
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
        public float2 Goal;
        public int Group;
        public GivingWay Rule;
        public float2 Push;

        /// <summary>A mover touched an arrived groupmate lying toward its goal, so it has arrived too.</summary>
        public bool ReachedGroup;

        public SeparationVisitor(Entity self, float2 position, float2 axis, in NavAgent agent, byte faction,
            bool moving, in MoveDestination destination, in GivingWay rule)
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

            var offset = entry.ClosestOffset(Position, SegmentAxis);
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
                // Only the mover gives way to a busy or enemy unit, so a truck can't shove a dozer that is building.
                var share = Rule.Yields(entry.Entity, entry.Faction, Faction) ? 0.5f : 1f;
                Push += direction * (overlap * share);
            }
            else if (Rule.Yields(Self, Faction, entry.Faction))
            {
                Push += StepAside(direction, entry) * (overlap * 0.5f);
            }
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
