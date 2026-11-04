using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Common
{
    public partial struct SquadMember
    {
        /// <summary>The offset of living member <paramref name="rank"/> of <paramref name="count"/> in a ring round a halted squad.</summary>
        public static float2 SpreadSlot(int rank, int count)
        {
            if (count <= 1)
            {
                return float2.zero;
            }

            var angle = math.PI * 0.25f + rank * math.PI * 2f / count;
            return new float2(math.cos(angle), math.sin(angle)) * SpreadRadius;
        }

        /// <summary>
        /// The offset in a loose wedge pointing along <paramref name="heading"/>, snapped to one of
        /// <see cref="WedgeHeadings"/> so a squad on a curving path doesn't resend its offsets every frame: a point man,
        /// then pairs falling back to the sides.
        /// </summary>
        public static float2 WedgeSlot(int rank, int count, float2 heading)
        {
            var step = math.PI * 2f / WedgeHeadings;
            var angle = math.round(math.atan2(heading.y, heading.x) / step) * step;
            heading = new float2(math.cos(angle), math.sin(angle));
            var side = new float2(heading.y, -heading.x);
            var row = (rank + 1) / 2;
            var sign = (rank & 1) == 1 ? -1f : 1f;
            var middle = count / 2 * WedgeDepth * 0.5f;
            return heading * (middle - row * WedgeDepth) + side * (sign * row * WedgeWidth);
        }
    }
}
