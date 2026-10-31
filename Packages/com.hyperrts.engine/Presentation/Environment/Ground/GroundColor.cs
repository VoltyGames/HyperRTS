using HyperRTS.Core;
using HyperRTS.Presentation.Common;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Ground
{
    /// <summary>
    /// Publishes the map's top-down ground colour to all shaders, so foliage takes the colour of the ground it grows
    /// from at its roots. Map tools bake the picture; one per map scene.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("")]
    [Icon(HyperRTSIcons.Environment)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public sealed class GroundColor : PresentationBehaviour
    {
        internal static readonly int PictureId = Shader.PropertyToID("_HyperRTSGroundColor");
        internal static readonly int RectId = Shader.PropertyToID("_HyperRTSGroundRect");

        // Grass.compute's own names for the copy.
        private static readonly int KernelPictureId = Shader.PropertyToID("_Ground");
        private static readonly int KernelRectId = Shader.PropertyToID("_GroundArea");

        [Tooltip("Top-down albedo of the ground, north up.")]
        public Texture2D picture;

        [Tooltip("World XZ rectangle the picture covers: x and y are its minimum x and z, width and height in metres.")]
        public Rect area;

        private void OnEnable() => Publish();

        private void OnValidate()
        {
            if (isActiveAndEnabled)
            {
                Publish();
            }
        }

        /// <summary>Copies the picture and its rect to a kernel: compute shaders don't see globals. A zero rect while there is none.</summary>
        public static void Bind(ComputeShader compute, int kernel)
        {
            var picture = Shader.GetGlobalTexture(PictureId);
            var rect = Shader.GetGlobalVector(RectId);
            var on = picture != null && rect.z > 0f;
            compute.SetTexture(kernel, KernelPictureId, on ? picture : Texture2D.blackTexture);
            compute.SetVector(KernelRectId, on ? rect : Vector4.zero);
        }

        protected override void OnDisabled() => Clear();

        private void Publish()
        {
            if (picture == null || area.width <= 0f || area.height <= 0f)
            {
                Debug.LogError("HyperRTS: Ground Color needs a picture and an area with a size.", this);
                Clear();
                return;
            }

            Shader.SetGlobalTexture(PictureId, picture);
            Shader.SetGlobalVector(RectId, TerrainHeightInput.Rect(area.min, area.size));
        }

        /// <summary>The outside-a-map state: shaders read a zero rect as "no ground picture" and skip blending.</summary>
        private static void Clear()
        {
            Shader.SetGlobalTexture(PictureId, Texture2D.blackTexture);
            Shader.SetGlobalVector(RectId, Vector4.zero);
        }
    }
}
