using HyperRTS.Presentation.Animation.Clips;
using HyperRTS.Presentation.Animation.VAT;
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Editor.Animation
{
    /// <summary>
    /// Import settings for vertex animation texture sets: a model with <c>VAT/&lt;Model&gt;.vat.json</c> beside it
    /// keeps full-float UV1, and the textures its bake info names import exact.
    /// </summary>
    public sealed partial class VATImportRules : AssetPostprocessor
    {
        private const int MaxTextureSize = 16384;

        // Texture names by bake info path, so a folder's textures parse each bake info file once until it changes.
        [AutoStaticsCleanup]
        private static readonly Dictionary<string, (DateTime Written, string[] Textures)> Named = new();

        /// <summary>After a game's own import rules, so these settings win.</summary>
        public override int GetPostprocessOrder() => 1000;

        public static bool IsVATModel(string path) => File.Exists(VATBakeInfo.PathFor(path));

        /// <summary>True for a texture a bake info file in the same VAT folder names as its positions, normals or rotations.</summary>
        public static bool IsVATTexture(string path)
        {
            var folder = Path.GetDirectoryName(path);
            if (Path.GetFileName(folder) != VATBakeInfo.Folder)
            {
                return false;
            }

            var file = Path.GetFileName(path);
            foreach (var json in Directory.GetFiles(folder, "*" + VATBakeInfo.Extension))
            {
                if (Array.IndexOf(ReadTextures(json), file) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] ReadTextures(string json)
        {
            var written = File.GetLastWriteTimeUtc(json);
            if (!Named.TryGetValue(json, out var named) || named.Written != written)
            {
                var bakeInfo = JsonUtility.FromJson<VATBakeInfo>(File.ReadAllText(json));
                named = (written, new[] { bakeInfo.positions, bakeInfo.rotations });
                Named[json] = named;
            }

            return named.Textures;
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

        // Half-float u loses whole columns past 2048. Only the animated meshes carry the texel index: a sectioned
        // model's standing and broken meshes stay still.
        private void OnPostprocessModel(GameObject model)
        {
            if (!IsVATModel(assetPath))
            {
                return;
            }

            var uv = new List<Vector2>();
            var animated = 0;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (!mesh.HasVertexAttribute(VertexAttribute.TexCoord1))
                {
                    continue;
                }

                animated++;
                if (mesh.GetVertexAttributeFormat(VertexAttribute.TexCoord1) != VertexAttributeFormat.Float32)
                {
                    mesh.GetUVs(1, uv);
                    mesh.SetUVs(1, uv);
                }
            }

            if (animated == 0)
            {
                Debug.LogError($"[HyperRTS] VAT model {assetPath} has no mesh with a UV1 texel index.", model);
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
