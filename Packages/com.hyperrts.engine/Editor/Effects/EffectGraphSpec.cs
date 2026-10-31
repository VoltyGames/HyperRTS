using System;
using System.Collections.Generic;
using System.IO;
using HyperRTS.Editor.Common;
using Newtonsoft.Json.Linq;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace HyperRTS.Editor.Effects
{
    /// <summary>
    /// The hand-editable description of a VFX Graph rig, read from <c>&lt;Graph&gt;.effect.json</c> beside the
    /// <c>&lt;Graph&gt;.vfx</c> that <see cref="EffectGraphBuilder"/> writes from it.
    /// </summary>
    [Serializable]
    public sealed class EffectGraphSpec
    {
        public const string Extension = ".effect.json";

        // A layer as its class starts, which a save leaves out of an added layer.
        [NoAutoStaticsCleanup]
        private static readonly Lazy<IReadOnlyDictionary<string, JToken>> LayerDefaults = new(() =>
            new Dictionary<string, JToken> { [nameof(layers)] = JsonFormat.Parse(JsonFormat.Format(JsonUtility.ToJson(new EffectLayer()))) });

        public List<EffectLayer> layers = new();

        /// <summary>Reads a spec, rejecting unknown fields; <paramref name="source"/> names it in errors.</summary>
        public static EffectGraphSpec Parse(string json, string source)
        {
            var spec = new EffectGraphSpec();
            JsonFormat.ReadInto(json, spec, "effect graph", source);
            if (spec.layers.Count == 0)
            {
                throw new InvalidOperationException($"HyperRTS: {source} has no layers.");
            }

            return spec;
        }

        /// <summary>The spec in <see cref="JsonFormat"/>'s layout, with every field written.</summary>
        public string Write() => JsonFormat.Format(JsonUtility.ToJson(this));

        /// <summary>
        /// <paramref name="fileJson"/> with this spec written over it, changing only what differs (see
        /// <see cref="JsonChanges"/>), so a saved edit diffs like a hand edit.
        /// </summary>
        public string WriteOver(string fileJson, string source)
        {
            var before = JsonFormat.Parse(Parse(fileJson, source).Write());
            var after = JsonFormat.Parse(Write());
            return JsonFormat.Write(JsonChanges.Apply(JsonFormat.Parse(fileJson), before, after, LayerDefaults.Value));
        }

        public static bool IsRecipe(string path) => path.EndsWith(Extension, StringComparison.Ordinal);

        /// <summary>The graph a spec at <paramref name="jsonPath"/> builds.</summary>
        public static string GraphPath(string jsonPath)
        {
            if (!IsRecipe(jsonPath))
            {
                throw new ArgumentException($"HyperRTS: {jsonPath} is not an {Extension} file.");
            }

            return jsonPath.Substring(0, jsonPath.Length - Extension.Length) + ".vfx";
        }

        /// <summary>A stamp of the spec's text that ignores line endings, so a git checkout keeps it.</summary>
        public static string Stamp(string jsonPath)
        {
            var text = File.ReadAllText(jsonPath).Replace("\r\n", "\n");
            return Hash128.Compute(text).ToString();
        }
    }
}
