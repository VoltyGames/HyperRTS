using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Editor.Rendering
{
    /// <summary>
    /// The HyperRTS/LitTeam material inspector: its properties as plain fields, with each map's keyword following the
    /// map, as URP's Lit inspector does (the Detail toggle sets its own keyword), so a material edited by hand renders what it shows.
    /// </summary>
    public sealed class LitTeamGUI : ShaderGUI
    {
        private const MaterialGlobalIlluminationFlags Glowing =
            MaterialGlobalIlluminationFlags.RealtimeIndirectEmission | MaterialGlobalIlluminationFlags.RealtimeDirectEmission;

        private static readonly int BumpMap = Shader.PropertyToID("_BumpMap");
        private static readonly int MetallicGlossMap = Shader.PropertyToID("_MetallicGlossMap");
        private static readonly int OcclusionMap = Shader.PropertyToID("_OcclusionMap");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int AlphaClip = Shader.PropertyToID("_AlphaClip");
        private static readonly int Transparent = Shader.PropertyToID("_Transparent");
        private static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");
        private static readonly string[] DepthPasses = { "ShadowCaster", "DepthOnly", "DepthNormals", "MotionVectors" };

        public override void OnGUI(MaterialEditor editor, MaterialProperty[] properties)
        {
            EditorGUI.BeginChangeCheck();
            editor.PropertiesDefaultGUI(properties);
            if (EditorGUI.EndChangeCheck())
            {
                foreach (var target in editor.targets)
                {
                    ValidateMaterial((Material)target);
                }
            }
        }

        public override void ValidateMaterial(Material material)
        {
            // The detail normal tilts the tangent-space normal, so detail turns the normal path on.
            Follow(material, "_NORMALMAP", material.GetTexture(BumpMap) != null || material.IsKeywordEnabled("_DETAIL"));
            Follow(material, "_METALLICSPECGLOSSMAP", material.GetTexture(MetallicGlossMap) != null);
            Follow(material, "_OCCLUSIONMAP", material.GetTexture(OcclusionMap) != null);
            var glows = material.GetColor(EmissionColor).maxColorComponent > 0f;
            Follow(material, "_EMISSION", glows);
            material.globalIlluminationFlags = glows ? Glowing : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            var clipped = material.GetFloat(AlphaClip) > 0.5f;
            material.renderQueue = clipped ? (int)RenderQueue.AlphaTest : -1;
            FollowSurface(material);
        }

        /// <summary>
        /// A transparent material blends premultiplied, as URP Lit's Preserve Specular does: base alpha fades the
        /// diffuse while reflections stay whole, which is glass. It casts no shadow and stays out of the depth passes.
        /// </summary>
        private static void FollowSurface(Material material)
        {
            var transparent = material.GetFloat(Transparent) > 0.5f;
            Follow(material, "_SURFACE_TYPE_TRANSPARENT", transparent);
            Follow(material, "_ALPHAPREMULTIPLY_ON", transparent);
            material.SetFloat(SrcBlend, (float)BlendMode.One);
            material.SetFloat(DstBlend, (float)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            material.SetFloat(ZWrite, transparent ? 0f : 1f);
            material.SetOverrideTag("RenderType", transparent ? "Transparent" : "");
            foreach (var pass in DepthPasses)
            {
                material.SetShaderPassEnabled(pass, !transparent);
            }

            if (transparent)
            {
                material.renderQueue = (int)RenderQueue.Transparent;
            }
        }

        private static void Follow(Material material, string keyword, bool on) =>
            material.SetKeyword(new LocalKeyword(material.shader, keyword), on);
    }
}
