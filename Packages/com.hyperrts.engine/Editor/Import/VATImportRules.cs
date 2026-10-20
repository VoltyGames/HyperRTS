using System.Collections.Generic;
using System.IO;
using HyperRTS.Presentation.Animation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Editor.Import
{
    /// <summary>
    /// Import settings of vertex animation texture sets. A model is a VAT model when <c>VAT/&lt;Model&gt;.json</c> sits
    /// beside it, and its UV1 must reach the GPU as full floats. The textures that sidecar names are read texel for
    /// texel, so they stay exact.
    /// </summary>
    public sealed class VATImportRules : AssetPostprocessor
    {
        private const int MaxTextureSize = 16384;

        /// <summary>After a game's own import rules, so these settings win.</summary>
        public override int GetPostprocessOrder() => 1000;

        public static bool IsVATModel(string path) => File.Exists(VATSidecar.PathFor(path));

        /// <summary>True for a texture a sidecar in the same VAT folder names as its positions or normals.</summary>
        public static bool IsVATTexture(string path)
        {
            var folder = Path.GetDirectoryName(path);
            if (Path.GetFileName(folder) != VATSidecar.Folder)
            {
                return false;
            }

            var file = Path.GetFileName(path);
            foreach (var json in Directory.GetFiles(folder, "*.json"))
            {
                var sidecar = JsonUtility.FromJson<VATSidecar>(File.ReadAllText(json));
                if (sidecar.positions == file || sidecar.normals == file)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnPreprocessModel()
        {
            if (!IsVATModel(assetPath))
            {
                return;
            }

            // A generated lightmap UV would overwrite the texel index in UV1.
            var importer = (ModelImporter)assetImporter;
            importer.generateSecondaryUV = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
        }

        // Half-float u loses whole columns past 2048.
        private void OnPostprocessModel(GameObject model)
        {
            if (!IsVATModel(assetPath))
            {
                return;
            }

            var uv = new List<Vector2>();
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (!mesh.HasVertexAttribute(VertexAttribute.TexCoord1))
                {
                    Debug.LogError($"[HyperRTS] VAT model {assetPath} mesh '{mesh.name}' has no UV1 texel index.", model);
                    continue;
                }

                if (mesh.GetVertexAttributeFormat(VertexAttribute.TexCoord1) != VertexAttributeFormat.Float32)
                {
                    mesh.GetUVs(1, uv);
                    mesh.SetUVs(1, uv);
                }
            }
        }

        private void OnPreprocessTexture()
        {
            if (!IsVATTexture(assetPath))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.isReadable = false;
            importer.maxTextureSize = MaxTextureSize;
            // Uncompressed HDR source imports as RGBAHalf.
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
