using System;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Environment.Ground.Craters
{
    /// <summary>
    /// A building's footprint, over which the drawn terrain is levelled to its base so the ground its model sits on is
    /// never covered. The layout matches <c>BuildingLevel</c> in <c>CraterHeight.shader</c>.
    /// </summary>
    public readonly struct BuildingLevel : IEquatable<BuildingLevel>
    {
        /// <summary>Bytes of one element in the shader's buffer.</summary>
        public const int Stride = 32;

        /// <summary>Metres past the footprint over which the levelling eases out, so the ground blends back with no step.</summary>
        public const float Skirt = 2.5f;

        /// <summary>Metres the levelled ground sits below the building's base, so a model's apron never z-fights it.</summary>
        public const float Drop = 0.03f;

        public readonly float2 Center;
        public readonly float2 Right;
        public readonly float2 Half;
        public readonly float Target;
        private readonly float _padding;

        /// <param name="right">Unit direction of the footprint's width.</param>
        public BuildingLevel(float2 center, float2 right, float2 size, float baseHeight)
        {
            Center = center;
            Right = right;
            Half = size * 0.5f;
            Target = baseHeight - Drop;
            _padding = 0f;
        }

        private float2 Forward => new(-Right.y, Right.x);

        /// <summary>Half the width of the world-axis box around the footprint and its skirt.</summary>
        public float2 Reach => math.abs(Right) * Half.x + math.abs(Forward) * Half.y + Skirt;

        /// <summary>How far the ground at <paramref name="point"/> is levelled: 1 inside, easing to 0 across the skirt.</summary>
        public float Weight(float2 point)
        {
            var offset = point - Center;
            var local = new float2(math.dot(offset, Right), math.dot(offset, Forward));
            var gap = math.length(math.max(math.abs(local) - Half, 0f));
            var t = math.saturate(1f - gap / Skirt);
            return t * t * (3f - 2f * t);
        }

        public bool Equals(BuildingLevel other) =>
            Center.Equals(other.Center) && Right.Equals(other.Right) && Half.Equals(other.Half)
            && Target == other.Target;
    }
}
