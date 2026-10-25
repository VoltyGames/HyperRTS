using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>An XZ box turned about Y, as obstacles, areas and buildings stamp themselves onto the nav grid.</summary>
    public readonly struct NavBox
    {
        private const float AxisTolerance = 1e-3f;

        // Metres two boxes may share along an axis and still count as touching, so flush neighbours fit.
        private const float TouchTolerance = 1e-3f;

        public readonly float2 Center;
        public readonly float2 Right;
        public readonly float2 Forward;
        public readonly float2 Half;

        public NavBox(float3 position, quaternion rotation, float2 size)
        {
            Center = position.xz;
            Right = math.normalizesafe(math.mul(rotation, math.right()).xz, new float2(1f, 0f));
            Forward = math.normalizesafe(math.mul(rotation, math.forward()).xz, new float2(0f, 1f));
            Half = size * 0.5f;
        }

        /// <summary>Half size of the world-axis box around it.</summary>
        public float2 Extents => new(
            math.abs(Right.x) * Half.x + math.abs(Forward.x) * Half.y,
            math.abs(Right.y) * Half.x + math.abs(Forward.y) * Half.y);

        /// <summary>Turned by a multiple of 90°, so its world-axis box is the box itself.</summary>
        public bool IsAxisAligned => math.abs(Right.x * Right.y) < AxisTolerance;

        /// <summary>Whether a point lies inside the box grown by <paramref name="margin"/> on every side.</summary>
        public bool Contains(float2 point, float margin)
        {
            var offset = point - Center;
            return math.abs(math.dot(offset, Right)) <= Half.x + margin
                   && math.abs(math.dot(offset, Forward)) <= Half.y + margin;
        }

        /// <summary>Separating-axis test on both boxes' sides; boxes that only touch don't overlap.</summary>
        public bool Overlaps(in NavBox other)
        {
            var offset = other.Center - Center;
            return OverlapsAlong(other, offset, Right) && OverlapsAlong(other, offset, Forward)
                   && OverlapsAlong(other, offset, other.Right) && OverlapsAlong(other, offset, other.Forward);
        }

        /// <summary>Gap between the box and the segment from a to b, 0 when they touch.</summary>
        public float Gap(float2 a, float2 b) => NavCapsule.SegmentRectDistance(Local(a), Local(b), -Half, Half);

        /// <summary>The point <paramref name="offset"/> out from the edge nearest <paramref name="point"/>.</summary>
        public float2 EdgePoint(float2 point, float offset)
        {
            var local = Local(point);
            var edge = math.clamp(local, -Half, Half);
            var outward = math.normalizesafe(local - edge);
            if (outward.Equals(float2.zero))
            {
                outward = InsideExit(local);
                edge = math.select(local, Half * math.sign(outward), outward != 0f);
            }

            return Center + Right * (edge.x + outward.x * offset) + Forward * (edge.y + outward.y * offset);
        }

        /// <summary>Half the box's width along <paramref name="axis"/>.</summary>
        private float Reach(float2 axis) =>
            math.abs(math.dot(Right, axis)) * Half.x + math.abs(math.dot(Forward, axis)) * Half.y;

        private bool OverlapsAlong(in NavBox other, float2 offset, float2 axis) =>
            math.abs(math.dot(offset, axis)) < Reach(axis) + other.Reach(axis) - TouchTolerance;

        private float2 Local(float2 point)
        {
            var offset = point - Center;
            return new float2(math.dot(offset, Right), math.dot(offset, Forward));
        }

        // A point inside leaves by the nearest side.
        private float2 InsideExit(float2 local)
        {
            var room = Half - math.abs(local);
            var side = math.select(new float2(-1f), new float2(1f), local >= 0f);
            return room.x < room.y ? new float2(side.x, 0f) : new float2(0f, side.y);
        }
    }
}
