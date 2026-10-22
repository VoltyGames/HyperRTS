using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>
    /// Exponential height fog, thick in valleys and thinning with height, blended through each weather's Volume
    /// profile. <see cref="Rendering.HeightFogPass"/> publishes it as the HeightFog.hlsl globals.
    /// </summary>
    [VolumeComponentMenu("HyperRTS/Height Fog")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class HeightFog : VolumeComponent
    {
        private static readonly int ColorId = Shader.PropertyToID("_HyperRTSFogColor");
        private static readonly int SunColorId = Shader.PropertyToID("_HyperRTSFogSunColor");
        private static readonly int ParamsId = Shader.PropertyToID("_HyperRTSFogParams");
        private static readonly int ShapeId = Shader.PropertyToID("_HyperRTSFogShape");

        [Tooltip("Fog colour away from the sun.")]
        public ColorParameter color = new(new Color(0.70f, 0.76f, 0.84f), false, false, true);

        [Tooltip("Fog per metre of view at the base height.")]
        public MinFloatParameter density = new(0f, 0f);

        [Tooltip("World height (y) where the fog has its base density.")]
        public FloatParameter baseHeight = new(0f);

        [Tooltip("How fast the fog thins per metre above the base height.")]
        public MinFloatParameter heightFalloff = new(0.08f, 0.0001f);

        [Tooltip("Metres from the camera before fog starts.")]
        public MinFloatParameter startDistance = new(30f, 0f);

        [Tooltip("The most the fog ever covers, so far ground keeps some contrast.")]
        public ClampedFloatParameter maxOpacity = new(0f, 0f, 1f);

        [Tooltip("Colour added to the fog looking toward the sun.")]
        public ColorParameter sunColor = new(new Color(0.45f, 0.38f, 0.26f), false, false, true);

        [Tooltip("How tight the sun glow is: higher keeps it closer to the sun's direction.")]
        public MinFloatParameter sunExponent = new(8f, 1f);

        public bool IsActive() => active && density.value > 0f && maxOpacity.value > 0f;

        /// <summary>Sets the fog globals in linear colour; a hidden fog has zero opacity so transparents that apply it stay clear.</summary>
        public void Publish(RasterCommandBuffer cmd, bool shown)
        {
            cmd.SetGlobalVector(ColorId, color.value.linear);
            cmd.SetGlobalVector(SunColorId, sunColor.value.linear);
            cmd.SetGlobalVector(ParamsId, new Vector4(density.value, baseHeight.value, heightFalloff.value, startDistance.value));
            cmd.SetGlobalVector(ShapeId, new Vector4(shown ? maxOpacity.value : 0f, sunExponent.value, 0f, 0f));
        }
    }
}
