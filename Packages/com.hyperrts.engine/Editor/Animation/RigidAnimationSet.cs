using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Presentation.Animation.Clips;
using HyperRTS.Presentation.Animation.VAT;
using HyperRTS.Simulation.Common.Authoring;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Animation
{
    /// <summary>
    /// A model whose pieces move rigidly, such as a sectioned building, and its motion model, baked into the position
    /// and rotation textures and the clip table its pieces play.
    /// </summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Animation + "Rigid Animation Set", fileName = "RigidAnimationSet")]
    [HelpURL(HyperRTSDocs.Presentation)]
    public class RigidAnimationSet : ScriptableObject
    {
        public const string GeneratedLabel = BoneAnimationSet.GeneratedLabel;
        private const int MaxRows = 16384;
        private const int MaxTextureSize = 16384;

        [Tooltip("The model whose meshes carry each vertex's texture column in UV1.")]
        public GameObject model;

        [Tooltip("The motion model, VAT/<Model>_Motion.fbx: an empty a piece, keyed on every frame of one take.")]
        public GameObject motion;

        [Tooltip("The clip table the model's VAT renderers play; the bake writes its width, clips and bounds.")]
        public VATClipTable table;

        [Tooltip("Position texture the bake writes; empty writes T_<Set>_Pos.exr beside the set.")]
        public Texture2D positions;

        [Tooltip("Rotation texture the bake writes; empty writes T_<Set>_Rot.exr beside the set.")]
        public Texture2D rotations;

        private string Folder => Path.GetDirectoryName(AssetDatabase.GetAssetPath(this))!.Replace('\\', '/');

        /// <summary>Samples the motion's take at every row and writes the textures and the table; returns their paths.</summary>
        [ContextMenu("Bake")]
        public List<string> Bake()
        {
            Check();
            var pieces = RigidMotion.Find(motion);
            var rows = pieces.RowCount;
            if (rows > MaxRows || pieces.pieces.Count > MaxTextureSize)
            {
                throw new InvalidOperationException($"Rigid set '{name}' needs {pieces.pieces.Count} x {rows} texels, over the {MaxTextureSize} texture limit.");
            }

            var poses = RigidPoses.Sample(motion, TakeOf(motion), pieces, rows);
            var positionsPath = TexturePath(positions, "Pos");
            var rotationsPath = TexturePath(rotations, "Rot");
            positions = WriteTexture(poses.Positions(), positionsPath);
            rotations = WriteTexture(poses.Rotations(), rotationsPath);

            var bounds = poses.Bounds(model);
            table.width = pieces.pieces.Count;
            table.boundsMin = bounds.min;
            table.boundsMax = bounds.max;
            table.clips = pieces.clips.Select(clip => new NamedClip
            {
                name = clip.name, startRow = clip.startRow, frameCount = clip.frameCount, fps = clip.fps, loop = clip.loop,
            }).ToList();
            EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            return new List<string> { positionsPath, rotationsPath, AssetDatabase.GetAssetPath(table) };
        }

        private void Check()
        {
            if (model == null || motion == null || table == null)
            {
                throw new InvalidOperationException($"Rigid set '{name}' needs a model, a motion model and a clip table.");
            }
        }

        private static AnimationClip TakeOf(GameObject motionModel)
        {
            var path = AssetDatabase.GetAssetPath(motionModel);
            var takes = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal)).ToList();
            return takes.Count == 1 ? takes[0] : throw new InvalidOperationException($"HyperRTS: {path} needs exactly one take, but has {takes.Count}.");
        }

        private string TexturePath(Texture2D texture, string kind) =>
            texture != null ? AssetDatabase.GetAssetPath(texture) : $"{Folder}/T_{name}_{kind}.exr";

        // Half-float EXR, point sampled and uncompressed: the shader reads exact texels.
        private static Texture2D WriteTexture(Texture2D made, string path)
        {
            File.WriteAllBytes(path, made.EncodeToEXR(Texture2D.EXRFlags.CompressZIP));
            DestroyImmediate(made);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.isReadable = false;
            importer.maxTextureSize = MaxTextureSize;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var labels = AssetDatabase.GetLabels(texture);
            if (!labels.Contains(GeneratedLabel))
            {
                AssetDatabase.SetLabels(texture, labels.Append(GeneratedLabel).ToArray());
            }

            return texture;
        }
    }
}
