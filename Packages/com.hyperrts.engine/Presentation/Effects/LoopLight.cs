using HyperRTS.Core;
using HyperRTS.Presentation.Common;
using UnityEngine;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>
    /// A flickering point light a looping rig asks for, such as a fire's glow. Goes on the rig's root; the nearest
    /// streamed loops get one of <see cref="LoopLights"/>' pooled lights on High detail.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Effects + "Loop Light")]
    [Icon(HyperRTSIcons.Effects)]
    [HelpURL(HyperRTSDocs.Effects)]
    [DisallowMultipleComponent]
    public class LoopLight : PresentationBehaviour
    {
        [Tooltip("Colour of the light.")]
        public Color color = new(1f, 0.55f, 0.25f);

        [Tooltip("Light range in metres.")]
        public float range = 14f;

        [Tooltip("Intensity at full loop strength, between flickers.")]
        public float intensity = 6f;

        [Tooltip("Metres above the loop's socket the light sits.")]
        public float height = 1.5f;

        [Tooltip("Share of the intensity the flicker takes away at its deepest (0 to 1).")]
        [Range(0f, 1f)]
        public float flicker = 0.35f;

        [Tooltip("Flicker speed; 1 is a wood or fuel fire.")]
        public float flickerSpeed = 1f;

        /// <summary>The intensity at <paramref name="time"/>, offset by a per-source phase so fires don't pulse together.</summary>
        public float IntensityAt(float time, float phase, float strength)
        {
            var t = time * flickerSpeed;
            var wave = 0.5f + 0.3f * Mathf.Sin(t * 7.3f + phase) + 0.2f * Mathf.Sin(t * 17.9f + phase * 2.3f);
            return intensity * strength * (1f - flicker * wave);
        }
    }
}
