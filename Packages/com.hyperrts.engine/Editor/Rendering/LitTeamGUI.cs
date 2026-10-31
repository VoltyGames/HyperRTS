using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Editor.Rendering
{
    /// <summary>
    /// The HyperRTS/LitTeam material inspector: its properties as plain fields, with each map's keyword following the
    /// map, as URP's Lit inspector does, so a material edited by hand renders what it shows.
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
            Follow(material, "_NORMALMAP", material.GetTexture(BumpMap) != null);
            Follow(material, "_METALLICSPECGLOSSMAP", material.GetTexture(MetallicGlossMap) != null);
            Follow(material, "_OCCLUSIONMAP", material.GetTexture(OcclusionMap) != null);
            var glows = material.GetColor(EmissionColor).maxColorComponent > 0f;
            Follow(material, "_EMISSION", glows);
            material.globalIlluminationFlags = glows ? Glowing : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            var clipped = material.GetFloat(AlphaClip) > 0.5f;
            material.renderQueue = clipped ? (int)RenderQueue.AlphaTest : -1;
        }

        private static void Follow(Material material, string keyword, bool on) =>
            material.SetKeyword(new LocalKeyword(material.shader, keyword), on);
    }
}
