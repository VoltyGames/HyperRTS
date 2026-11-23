using System;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Environment.Weather
{
    /// <summary>The weather's volumetric fog as the density texture meets it: its base density and how it thins with height.</summary>
    public readonly struct FogShape : IEquatable<FogShape>
    {
        /// <summary>Extinction per metre at <see cref="BaseHeight"/>, which the density texture multiplies.</summary>
        public readonly float Density;

        public readonly float BaseHeight;

        /// <summary>Metres over which the density falls to 1/e above <see cref="BaseHeight"/>.</summary>
        public readonly float ScaleHeight;

        public FogShape(float density, float baseHeight, float scaleHeight)
        {
            Density = math.max(density, 1e-5f);
            BaseHeight = baseHeight;
            ScaleHeight = math.max(scaleHeight, 0.01f);
        }

        /// <summary>The share of <see cref="Density"/> the fog keeps at world <paramref name="height"/>.</summary>
        public float Multiplier(float height) => math.exp(-math.max(height - BaseHeight, 0f) / ScaleHeight);

        public bool Equals(FogShape other) =>
            Density == other.Density && BaseHeight == other.BaseHeight && ScaleHeight == other.ScaleHeight;

        public override bool Equals(object other) => other is FogShape shape && Equals(shape);

        public override int GetHashCode() => HashCode.Combine(Density, BaseHeight, ScaleHeight);
    }
}
