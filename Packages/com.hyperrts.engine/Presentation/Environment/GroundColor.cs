using HyperRTS.Core;
using UnityEngine;

namespace HyperRTS.Presentation.Environment
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
    public sealed class GroundColor : MonoBehaviour
    {
        private static readonly int PictureId = Shader.PropertyToID("_HyperRTSGroundColor");
        private static readonly int RectId = Shader.PropertyToID("_HyperRTSGroundRect");

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

        private void OnDisable() => Clear();

        private void Publish()
        {
            if (picture == null || area.width <= 0f || area.height <= 0f)
            {
                Debug.LogError("HyperRTS: Ground Color needs a picture and an area with a size.", this);
                Clear();
                return;
            }

            Shader.SetGlobalTexture(PictureId, picture);
            Shader.SetGlobalVector(RectId, new Vector4(area.xMin, area.yMin, 1f / area.width, 1f / area.height));
        }

        /// <summary>The outside-a-map state: shaders read a zero rect as "no ground picture" and skip blending.</summary>
        private static void Clear()
        {
            Shader.SetGlobalTexture(PictureId, Texture2D.blackTexture);
            Shader.SetGlobalVector(RectId, Vector4.zero);
        }
    }
}
