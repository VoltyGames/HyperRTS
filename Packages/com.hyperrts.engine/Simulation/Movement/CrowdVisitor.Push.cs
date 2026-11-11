using HyperRTS.Simulation.Navigation.Spatial;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Movement
{
    internal partial struct CrowdVisitor
    {
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
            var self = Unit.Entity;
            var pair = new int2(math.min(self.Index, other.Index), math.max(self.Index, other.Index));
            math.sincos(math.hash(pair) * (math.PI * 2f / uint.MaxValue), out var sin, out var cos);
            return new float2(cos, sin) * (self.Index < other.Index ? 1f : -1f);
        }
    }
}
