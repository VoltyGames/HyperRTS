using UnityEngine;

namespace HyperRTS.Presentation.Environment.Grass
{
    /// <summary>The quality knobs a camera's grass is scattered for, from <see cref="GrassRenderer"/>.</summary>
    internal readonly struct GrassSettings
    {
        public readonly float Density;
        public readonly float Distance;
        public readonly float ShadowDistance;

        public GrassSettings(float density, float distance, float shadowDistance)
        {
            Density = density;
            Distance = distance;
            ShadowDistance = shadowDistance;
        }

        /// <summary>The knobs as one value, to tell whether they changed since the last scatter.</summary>
        public Vector4 Packed => new(Density, Distance, ShadowDistance, 0f);
    }
}
