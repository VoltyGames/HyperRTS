using System.Linq;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>The UnitSilhouetteFeature on the active URP pipeline's renderers, for validation.</summary>
    public static class UnitSilhouettes
    {
        /// <summary>The feature, or null when no renderer of the active pipeline has one.</summary>
        public static UnitSilhouetteFeature Find()
        {
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset pipeline)
            {
                return null;
            }

            return pipeline.rendererDataList.ToArray()
                .Where(data => data != null)
                .SelectMany(data => data.rendererFeatures)
                .OfType<UnitSilhouetteFeature>()
                .FirstOrDefault();
        }
    }
}
