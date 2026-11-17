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

        public readonly TurnedRect Area;
        public readonly float Target;
        private readonly float _padding;

        public BuildingLevel(TurnedRect area, float baseHeight)
        {
            Area = area;
            Target = baseHeight - Drop;
            _padding = 0f;
        }

        /// <summary>Half the width of the world-axis box around the footprint and its skirt.</summary>
        public float2 Reach => Area.Extent + Skirt;

        public bool Equals(BuildingLevel other) => Area.Equals(other.Area) && Target == other.Target;
    }
}
