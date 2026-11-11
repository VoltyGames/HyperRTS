using HyperRTS.Presentation.Common;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Weather
{
    /// <summary>Shows its children (puddle decals) only while the ground is at least <see cref="threshold"/> wet.</summary>
    [ExecuteAlways]
    public sealed class WetnessGroup : PresentationBehaviour
    {
        [Tooltip("Wetness (0 dry, 1 soaked) from which the children show.")]
        [Range(0f, 1f)]
        public float threshold = 0.5f;

        private static readonly int WetnessId = Shader.PropertyToID("_HyperRTSWetness");

        private bool? _shown;

        private void OnEnable() => _shown = null;

        // The shader global, not the match singleton, so puddles also show in edit mode and in the map's view captures.
        private void Update() => Apply(Shader.GetGlobalFloat(WetnessId));

        /// <summary>Shows or hides the children for this wetness, touching them only when it crosses the threshold.</summary>
        public void Apply(float level)
        {
            var shown = level >= threshold;
            if (shown == _shown)
            {
                return;
            }

            _shown = shown;
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(shown);
            }
        }
    }
}
