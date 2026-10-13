using UnityEngine;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>
    /// Publishes where foliage plants shrink to their roots (<c>_HyperRTSDetailFade</c>), ending at the terrain's detail
    /// distance so grass and crops are gone before the terrain culls them instead of popping out.
    /// </summary>
    public static class DetailFade
    {
        /// <summary>The share of the detail distance over which plants shrink away.</summary>
        public const float Band = 0.25f;

        private static readonly int FadeId = Shader.PropertyToID("_HyperRTSDetailFade");

        public static void Publish(float detailDistance) =>
            Shader.SetGlobalVector(FadeId, new Vector4(detailDistance * (1f - Band), detailDistance, 0f, 0f));
    }
}
