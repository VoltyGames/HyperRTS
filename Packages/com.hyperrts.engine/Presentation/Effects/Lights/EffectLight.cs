using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Common.Authoring;
using UnityEngine;

namespace HyperRTS.Presentation.Effects.Lights
{
    /// <summary>A brief point-light flash a rig's burst asks for, drawn by a pooled light on High detail.</summary>
    [AddComponentMenu(HyperRTSMenu.Look + "Effects/Effect Light")]
    [Icon(HyperRTSIcons.EffectLight)]
    [HelpURL(HyperRTSDocs.Effects)]
    [DisallowMultipleComponent]
    public class EffectLight : PresentationBehaviour
    {
        [Tooltip("Colour of the flash.")]
        public Color color = new(1f, 0.75f, 0.45f);

        [Tooltip("Light range.")]
        [Unit("m")]
        public float range = 12f;

        [Tooltip("Intensity at the flash's peak.")]
        public float intensity = 8f;

        [Tooltip("Seconds the flash lasts.")]
        public float duration = 0.15f;

        [Tooltip("Intensity over the flash's life (0 to 1), as a fraction of the peak.")]
        public AnimationCurve falloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    }
}
