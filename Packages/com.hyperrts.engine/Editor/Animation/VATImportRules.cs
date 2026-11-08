using System.Collections.Generic;
using System.IO;
using HyperRTS.Editor.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Editor.Animation
{
    /// <summary>
    /// Import settings for rigid vertex animation: a model with <c>VAT/&lt;Model&gt;_Motion.fbx</c> beside it keeps
    /// full-float UV1, and the motion model imports its take unreduced and keeps its pieces and clips inside it.
    /// </summary>
    public sealed class VATImportRules : AssetPostprocessor
    {
        private RigidMotion _motion;

        /// <summary>After a game's own import rules, so these settings win.</summary>
        public override int GetPostprocessOrder() => 1000;

        public static bool IsVATModel(string path) => File.Exists(RigidMotion.PathFor(path));

        private void OnPreprocessModel()
        {
            var importer = (ModelImporter)assetImporter;
            if (RigidMotion.IsMotionModel(assetPath))
            {
                // Every frame is keyed; reduced or Euler curves would move pieces between the keys the bake samples.
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.importAnimation = true;
                importer.resampleCurves = true;
                importer.animationCompression = ModelImporterAnimationCompression.Off;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importBlendShapes = false;
                importer.importCameras = false;
                importer.importLights = false;
                return;
            }

            if (!IsVATModel(assetPath))
            {
                return;
            }

            // A generated lightmap UV would overwrite the texel index in UV1.
            importer.generateSecondaryUV = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
        }

        private void OnPostprocessGameObjectWithUserProperties(GameObject node, string[] names, object[] values)
        {
            if (!RigidMotion.IsMotionModel(assetPath))
            {
                return;
            }

            if (_motion == null)
            {
                _motion = ScriptableObject.CreateInstance<RigidMotion>();
                _motion.name = "Motion";
            }

            _motion.Read(node, new FbxProperties(node.name, names, values));
        }

        private void OnPostprocessModel(GameObject model)
        {
            if (_motion != null)
            {
                _motion.Check(model, assetPath);
                context.AddObjectToAsset("motion", _motion);
                return;
            }

            if (IsVATModel(assetPath))
            {
                KeepFullFloatIndex(model);
            }
        }

        // Half-float u loses whole columns past 2048. Only the animated meshes carry the texel index: a sectioned
        // model's standing and broken meshes stay still.
        private void KeepFullFloatIndex(GameObject model)
        {
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
    }
}
