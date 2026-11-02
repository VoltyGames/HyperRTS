using UnityEngine;

namespace HyperRTS.Presentation.Environment.Vegetation.Scatter
{
    /// <summary>The quality knobs a camera's plants are scattered for. Crops cast shadows only from bands that end within the shadow distance.</summary>
    internal readonly struct ScatterSettings
    {
        public readonly float Density;
        public readonly float Distance;
        public readonly float ShadowDistance;

        public ScatterSettings(float density, float distance, float shadowDistance)
        {
            Density = density;
            Distance = distance;
            ShadowDistance = shadowDistance;
        }

        /// <summary>The knobs as one value, to tell whether they changed since the last scatter.</summary>
        public Vector4 Packed => new(Density, Distance, ShadowDistance, 0f);
    }
}
