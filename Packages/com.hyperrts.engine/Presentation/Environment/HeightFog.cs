using HyperRTS.Core;
using UnityEngine;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>
    /// Exponential height fog: thick in valleys, thinning with height. Publishes the HeightFog.hlsl globals that the
    /// height fog render pass and transparent shaders read. The last enabled one is current.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu(HyperRTSMenu.Environment + "Height Fog")]
    [Icon(HyperRTSIcons.Environment)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public sealed class HeightFog : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_HyperRTSFogColor");
        private static readonly int SunColorId = Shader.PropertyToID("_HyperRTSFogSunColor");
        private static readonly int ParamsId = Shader.PropertyToID("_HyperRTSFogParams");
        private static readonly int ShapeId = Shader.PropertyToID("_HyperRTSFogShape");

        [Tooltip("Fog colour away from the sun.")]
        public Color color = new(0.70f, 0.76f, 0.84f);

        [Tooltip("Fog per metre of view at the base height.")]
        [Min(0f)]
        public float density = 0.004f;

        [Tooltip("World height (y) where the fog has its base density.")]
        public float baseHeight;

        [Tooltip("How fast the fog thins per metre above the base height.")]
        [Min(0.0001f)]
        public float heightFalloff = 0.08f;

        [Tooltip("Metres from the camera before fog starts.")]
        [Min(0f)]
        public float startDistance = 30f;

        [Tooltip("The most the fog ever covers, so far ground keeps some contrast.")]
        [Range(0f, 1f)]
        public float maxOpacity = 0.6f;

        [Tooltip("Colour added to the fog looking toward the sun.")]
        public Color sunColor = new(0.45f, 0.38f, 0.26f);

        [Tooltip("How tight the sun glow is: higher keeps it closer to the sun's direction.")]
        [Min(1f)]
        public float sunExponent = 8f;

        /// <summary>The fog in effect; null when none is enabled, and the fog pass then draws nothing.</summary>
        public static HeightFog Current { get; private set; }

        private void OnEnable()
        {
            Current = this;
            Publish();
        }

        private void OnDisable()
        {
            if (Current != this)
            {
                return;
            }

            Current = null;
            Clear();
        }

        private void OnValidate()
        {
            if (Current == this)
            {
                Publish();
            }
        }

        private void Publish()
        {
            // SetGlobalColor converts to linear itself.
            Shader.SetGlobalColor(ColorId, color);
            Shader.SetGlobalColor(SunColorId, sunColor);
            Shader.SetGlobalVector(ParamsId, new Vector4(density, baseHeight, heightFalloff, startDistance));
            Shader.SetGlobalVector(ShapeId, new Vector4(maxOpacity, sunExponent, 0f, 0f));
        }

        // Zero opacity leaves transparent shaders that apply the fog unfogged.
        private static void Clear() => Shader.SetGlobalVector(ShapeId, Vector4.zero);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
            Clear();
        }
    }
}
