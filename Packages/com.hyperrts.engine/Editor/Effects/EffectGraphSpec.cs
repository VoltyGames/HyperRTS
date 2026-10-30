using System;
using System.Collections.Generic;
using System.IO;
using HyperRTS.Editor.Maps.Json;
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

        public List<EffectLayer> layers = new();

        /// <summary>Reads a spec, rejecting unknown fields; <paramref name="source"/> names it in errors.</summary>
        public static EffectGraphSpec Parse(string json, string source)
        {
            var spec = new EffectGraphSpec();
            MapJsonFormat.ReadInto(json, spec, "effect graph", source);
            if (spec.layers.Count == 0)
            {
                throw new InvalidOperationException($"HyperRTS: {source} has no layers.");
            }

            return spec;
        }

        /// <summary>The graph a spec at <paramref name="jsonPath"/> builds.</summary>
        public static string GraphPath(string jsonPath)
        {
            if (!jsonPath.EndsWith(Extension, StringComparison.Ordinal))
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
