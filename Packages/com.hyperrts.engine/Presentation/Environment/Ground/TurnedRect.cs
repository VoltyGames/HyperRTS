using System;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Environment.Ground
{
    /// <summary>A rectangle on the ground plane turned to face any way: its centre, its width axis and its half sizes.</summary>
    public readonly struct TurnedRect : IEquatable<TurnedRect>
    {
        public readonly float2 Center;
        public readonly float2 Right;
        public readonly float2 Half;

        /// <param name="right">Unit direction of the rectangle's width.</param>
        public TurnedRect(float2 center, float2 right, float2 size)
        {
            Center = center;
            Right = right;
            Half = size * 0.5f;
        }

        /// <summary>The rectangle of <paramref name="size"/> centred on <paramref name="center"/>, turned with the transform's right axis.</summary>
        public static TurnedRect Create(in float4x4 place, float2 center, float2 size)
        {
            var right = math.normalizesafe(place.c0.xz, new float2(1f, 0f));
            return new TurnedRect(center, right, size);
        }

        public float2 Forward => new(-Right.y, Right.x);

        /// <summary>Half the width of the world-axis box around the rectangle.</summary>
        public float2 Extent => math.abs(Right) * Half.x + math.abs(Forward) * Half.y;

        public bool Contains(float2 point)
        {
            var offset = point - Center;
            return math.abs(math.dot(offset, Right)) <= Half.x && math.abs(math.dot(offset, Forward)) <= Half.y;
        }

        public bool Equals(TurnedRect other) =>
            Center.Equals(other.Center) && Right.Equals(other.Right) && Half.Equals(other.Half);
    }
}
