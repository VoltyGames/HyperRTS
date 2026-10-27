using HyperRTS.Simulation.Common;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Where a hull's rear axle stands on XZ and which way the hull faces.</summary>
    public struct AxlePose
    {
        public float2 Position;
        public float2 Facing;

        public AxlePose(float2 position, float2 facing)
        {
            Position = position;
            Facing = facing;
        }

        /// <summary>
        /// Rolls the rear axle <paramref name="length"/> metres (negative in reverse) along the arc of
        /// <paramref name="curvature"/>, positive to the left.
        /// </summary>
        public readonly AxlePose Advance(float curvature, float length)
        {
            var turn = curvature * length;
            if (math.abs(turn) < 1e-5f)
            {
                return new AxlePose(Position + Facing * length, Facing);
            }

            math.sincos(turn, out var sin, out var cos);
            var left = Planar.Left(Facing);
            var moved = (Facing * sin + left * (1f - cos)) / curvature;
            var facing = Planar.Rotate(Facing, sin, cos);
            return new AxlePose(Position + moved, facing);
        }

        /// <summary>Where a step ends from this pose, given as where it ends from the origin facing +x.</summary>
        public readonly AxlePose Follow(in AxlePose step)
        {
            var left = Planar.Left(Facing);
            var position = Position + Facing * step.Position.x + left * step.Position.y;
            return new AxlePose(position, Facing * step.Facing.x + left * step.Facing.y);
        }
    }
}
