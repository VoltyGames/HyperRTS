using System;
using System.Collections.Generic;
using System.IO;
using HyperRTS.Core;
using UnityEngine;

namespace HyperRTS.Editor.Effects
{
    /// <summary>
    /// The hand-editable description of a VFX Graph rig, <c>&lt;Graph&gt;.effect.asset</c> beside the
    /// <c>&lt;Graph&gt;.vfx</c> that <see cref="EffectGraphBuilder"/> writes from it.
    /// </summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Effects + "Effect Recipe", fileName = "VFX_New.effect")]
    public sealed class EffectRecipe : ScriptableObject
    {
        public const string Extension = ".effect.asset";

        [Tooltip("One VFX system per layer, built in order; a child layer names an earlier one as its parent.")]
        public List<EffectLayer> layers = new();

        public static bool IsRecipe(string path) => path.EndsWith(Extension, StringComparison.Ordinal);

        /// <summary>The graph the recipe at <paramref name="recipePath"/> builds.</summary>
        public static string GraphPath(string recipePath)
        {
            if (!IsRecipe(recipePath))
            {
                throw new ArgumentException($"HyperRTS: {recipePath} is not an {Extension} file.");
            }

            return recipePath.Substring(0, recipePath.Length - Extension.Length) + ".vfx";
        }

        /// <summary>A stamp of the recipe's text that ignores line endings, so a git checkout keeps it.</summary>
        public static string Stamp(string recipePath)
        {
            var text = File.ReadAllText(recipePath).Replace("\r\n", "\n");
            return Hash128.Compute(text).ToString();
        }
    }
}
