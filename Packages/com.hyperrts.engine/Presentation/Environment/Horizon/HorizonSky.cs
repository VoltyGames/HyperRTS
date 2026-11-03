using System;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Horizon
{
    /// <summary>
    /// The horizon's fade into the sky: over the ring's outer share its ground and belts take the sky's own horizon
    /// colour, read from the scene's HyperRTS/Sky skybox each render, so the ring meets the sky without a seam.
    /// </summary>
    [Serializable]
    public sealed class HorizonSky
    {
        private const string SkyShader = "HyperRTS/Sky";

        private static readonly int TexId = Shader.PropertyToID("_Tex");
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        private static readonly int RotationId = Shader.PropertyToID("_Rotation");
        private static readonly int HorizonBlurId = Shader.PropertyToID("_HorizonBlur");

        private static readonly int SkyTexId = Shader.PropertyToID("_SkyTex");
        private static readonly int SkyTintId = Shader.PropertyToID("_SkyTint");
        private static readonly int SkyExposureId = Shader.PropertyToID("_SkyExposure");
        private static readonly int SkyRotationId = Shader.PropertyToID("_SkyRotation");
        private static readonly int SkyBlurId = Shader.PropertyToID("_SkyBlur");
        private static readonly int SkyFadeId = Shader.PropertyToID("_SkyFade");
        private static readonly int TerrainRectId = Shader.PropertyToID("_TerrainRect");

        [Tooltip("Outer share of the ring over which the ground fades into the sky's horizon colour.")]
        [Range(0f, 1f)]
        public float fadeShare = 0.4f;

        /// <summary>Why the skybox can't lend the horizon its colour, or null when it can.</summary>
        public string Problem(Material skybox)
        {
            if (skybox == null)
            {
                return "the scene has no skybox";
            }

            return skybox.shader.name == SkyShader ? null : $"the skybox '{skybox.name}' isn't drawn by {SkyShader}";
        }

        /// <summary>Where on a horizon material the fade runs: the ring's outer share past the terrain.</summary>
        public void SetReach(Material material, Rect terrain, float width)
        {
            material.SetVector(SkyFadeId, new Vector4(width * (1f - fadeShare), width, 0f, 0f));
            material.SetVector(TerrainRectId, new Vector4(terrain.xMin, terrain.yMin, terrain.xMax, terrain.yMax));
        }

        /// <summary>Copies the skybox's look onto a horizon material, as HyperRTS/Sky reads it.</summary>
        public void Apply(Material skybox, Material material)
        {
            material.SetTexture(SkyTexId, skybox.GetTexture(TexId));
            material.SetColor(SkyTintId, skybox.GetColor(TintId));
            material.SetFloat(SkyExposureId, skybox.GetFloat(ExposureId));
            material.SetFloat(SkyRotationId, skybox.GetFloat(RotationId));
            material.SetFloat(SkyBlurId, skybox.GetFloat(HorizonBlurId));
        }
    }
}
