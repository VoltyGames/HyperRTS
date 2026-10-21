using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    /// <summary>
    /// Sums how far a unit must move to stop crowding neighbouring units (half each, as both move). Blockers are
    /// capsules (circles when the segment is zero) and must stay the larger of the two spacings apart, pushed along
    /// the line between their closest points. A blocker (a wreck) takes no share and pushes the unit fully
    /// clear. Units on layers that never share a surface (a ship under a bridge, a tank on it) ignore each other.
    /// </summary>
    internal struct SeparationVisitor : ISpatialVisitor
    {
        public Entity Self;
        public float2 Position;
        public float Radius;
        public float2 SegmentAxis;
        public float Spacing;
        public NavLayer Layer;
        public float2 Push;

        public SeparationVisitor(Entity self, float2 position, float2 axis, in NavAgent agent)
        {
            Self = self;
            Position = position;
            Radius = agent.Radius;
            SegmentAxis = axis;
            Spacing = agent.Spacing;
            Layer = agent.Layer;
            Push = float2.zero;
        }

        /// <summary>Where to query round; queries only read XZ.</summary>
        public readonly float3 Center => new(Position.x, 0f, Position.y);

        public readonly float Reach => Radius + math.length(SegmentAxis) + Spacing;

        public void Visit(in SpatialEntry entry)
        {
            var crowds = entry.IsAgent || entry.IsBlocker;
            if (!crowds || entry.Entity == Self)
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
            // A blocker never moves, so the unit takes the whole push.
            var share = entry.IsBlocker ? 1f : 0.5f;
            Push += Direction(offset, distance, entry) * ((reach - distance) * share);
        }

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
