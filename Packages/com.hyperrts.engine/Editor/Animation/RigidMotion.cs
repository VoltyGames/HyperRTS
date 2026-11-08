using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Presentation.Animation.Clips;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Animation
{
    /// <summary>
    /// The pieces and clips of a motion model, <c>VAT/&lt;Model&gt;_Motion.fbx</c> beside a rigid VAT model, read from
    /// its FBX properties on import and kept inside it. A <see cref="RigidAnimationSet"/> bakes the motion.
    /// </summary>
    public sealed class RigidMotion : ScriptableObject
    {
        public const string Folder = "VAT";
        public const string Suffix = "_Motion";

        /// <summary>The FBX property naming a piece's texture column.</summary>
        public const string ColumnProperty = "column";

        /// <summary>The FBX property naming a clip; its rows are in <c>startRow</c>, <c>frameCount</c> and <c>fps</c>.</summary>
        public const string ClipProperty = "clip";

        [Serializable]
        public struct Piece
        {
            [Tooltip("The empty's path under the model root, which the take's curves name.")]
            public string node;

            [Tooltip("The texture column the piece's vertices read.")]
            public int column;
        }

        [Tooltip("Every piece, by column.")]
        public List<Piece> pieces = new();

        [Tooltip("The clips, by first row.")]
        public List<NamedClip> clips = new();

        /// <summary>The motion model a rigid VAT model at <paramref name="modelPath"/> would have, in its format.</summary>
        public static string PathFor(string modelPath) =>
            Path.Combine(Path.GetDirectoryName(modelPath) ?? "", Folder, Path.GetFileNameWithoutExtension(modelPath) + Suffix + Path.GetExtension(modelPath))
                .Replace('\\', '/');

        public static bool IsMotionModel(string path) =>
            Path.GetFileName(Path.GetDirectoryName(path)) == Folder
            && Path.GetFileNameWithoutExtension(path).EndsWith(Suffix, StringComparison.Ordinal);

        /// <summary>The motion inside <paramref name="model"/>, or an error naming what is missing.</summary>
        public static RigidMotion Find(GameObject model)
        {
            var path = AssetDatabase.GetAssetPath(model);
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<RigidMotion>().FirstOrDefault()
                ?? throw new InvalidOperationException($"HyperRTS: {path} has no piece motion; is it a VAT/<Model>{Suffix}.fbx with '{ColumnProperty}' properties?");
        }

        /// <summary>Rows in the textures: up to the end of the last clip.</summary>
        public int RowCount => clips.Max(clip => clip.startRow + clip.frameCount);

        /// <summary>Adds what one FBX node's properties say: a piece, a clip or nothing.</summary>
        public void Read(GameObject node, FbxProperties properties)
        {
            if (properties.TryInt(ColumnProperty, out var column))
            {
                // Unity calls this before it parents the node; Check turns the name into a path.
                pieces.Add(new Piece { node = node.name, column = column });
            }
            else if (properties.TryString(ClipProperty, out var clip))
            {
                clips.Add(new NamedClip
                {
                    name = clip,
                    startRow = properties.Int("startRow"),
                    frameCount = properties.Int("frameCount"),
                    fps = properties.Float("fps"),
                    loop = false,
                });
            }
        }

        /// <summary>
        /// Sorts pieces and clips, turns each piece's node name into its path under <paramref name="model"/>, and
        /// throws unless the columns run 0 to n - 1 and some clip exists.
        /// </summary>
        public void Check(GameObject model, string source)
        {
            var paths = model.GetComponentsInChildren<Transform>(true).ToDictionary(node => node.name, node => AnimationUtility.CalculateTransformPath(node, model.transform));
            for (var i = 0; i < pieces.Count; i++)
            {
                pieces[i] = new Piece { node = paths[pieces[i].node], column = pieces[i].column };
            }

            pieces.Sort((a, b) => a.column.CompareTo(b.column));
            clips.Sort((a, b) => a.startRow.CompareTo(b.startRow));
            for (var i = 0; i < pieces.Count; i++)
            {
                if (pieces[i].column != i)
                {
                    throw new InvalidDataException($"HyperRTS: {source} has no piece for column {i}.");
                }
            }

            if (clips.Count == 0)
            {
                throw new InvalidDataException($"HyperRTS: {source} has pieces but no node with a '{ClipProperty}' property.");
            }
        }
    }
}
