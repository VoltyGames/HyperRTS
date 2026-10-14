using UnityEngine;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>Shows its children (puddle decals) only while the ground is at least <see cref="threshold"/> wet.</summary>
    [ExecuteAlways]
    public sealed class WetnessGroup : MonoBehaviour
    {
        [Tooltip("Wetness (0 dry, 1 soaked) from which the children show.")]
        [Range(0f, 1f)]
        public float threshold = 0.5f;

        public bool Shown => Wetness.Level >= threshold;

        private void OnEnable()
        {
            Wetness.Changed += Apply;
            Apply();
        }

        private void OnDisable()
        {
            Wetness.Changed -= Apply;
        }

        public void Apply()
        {
            var shown = Shown;
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(shown);
            }
        }
    }
}
