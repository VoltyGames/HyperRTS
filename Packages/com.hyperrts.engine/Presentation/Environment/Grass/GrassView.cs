using UnityEngine;

namespace HyperRTS.Presentation.Environment.Grass
{
    /// <summary>The grass kernel's distance rules on the CPU: thinning, bands and the buffer room they need.</summary>
    public static class GrassView
    {
        /// <summary>Near plants cast shadows, far ones don't.</summary>
        public const int Bands = 2;
        public const int Near = 0;
        public const int Far = 1;

        /// <summary>Share of the draw distance from which plants thin out.</summary>
        public const float ThinStart = 0.3f;

        /// <summary>Share of plants left at the draw distance.</summary>
        public const float FarKeep = 0.3f;

        /// <summary>The most a far plant grows to cover for the thinned ones round it.</summary>
        public const float MaxGrow = 1.5f;

        /// <summary>Share of the draw distance over which the last plants shrink away.</summary>
        public const float FadeShare = 0.12f;

        /// <summary>Share of the keep threshold over which a thinned plant shrinks before it goes.</summary>
        public const float KeepEdge = 0.15f;

        /// <summary>Thinning as Grass.compute's <c>_Thinning</c> reads it.</summary>
        public static Vector4 Thinning => new(ThinStart, FarKeep, MaxGrow, FadeShare);

        /// <summary>Share of plants kept <paramref name="range"/> metres from the camera.</summary>
        public static float Keep(float range, float distance)
        {
            var start = ThinStart * distance;
            return Mathf.Lerp(1f, FarKeep, Mathf.Clamp01((range - start) / (distance - start)));
        }

        /// <summary>How much a plant kept at <paramref name="keep"/> grows: enough to cover the same ground.</summary>
        public static float Grow(float keep) => Mathf.Min(1f / Mathf.Sqrt(keep), MaxGrow);

        /// <summary>Share of a tile's plants drawn when its nearest point is <paramref name="near"/> metres away.</summary>
        public static float Drawn(float near, float distance) => Mathf.Min(1f, Keep(near, distance) * (1f + KeepEdge));

        /// <summary>Whether plants between <paramref name="near"/> and <paramref name="far"/> metres can fall in a band.</summary>
        public static bool InBand(int band, float shadowDistance, float near, float far) =>
            band == Near ? near < shadowDistance : far >= shadowDistance;

        /// <summary>Room for a slot expected to hold <paramref name="expected"/> plants, with three deviations of slack.</summary>
        public static int Capacity(float expected) =>
            Mathf.CeilToInt(expected * 1.1f + 3f * Mathf.Sqrt(expected) + 16f);
    }
}
