using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Presentation.Effects;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace HyperRTS.Editor.Effects
{
    /// <summary>
    /// Writes a rig's VFX Graph from its <see cref="EffectGraphSpec"/>: the Burst event, the Density property, the
    /// stream events and properties when a layer has a rate, and one system per layer, each placed where its spawn
    /// came from, thinned by Density and shaded by the fog of war. The graph is generated output, stamped with its
    /// spec so a stale one is found.
    /// </summary>
    public static class EffectGraphBuilder
    {
        private const string BuildMenu = "Assets/HyperRTS/Build Effect Graph";
        private const string BuildAllMenu = "Assets/HyperRTS/Build All Effect Graphs";

        /// <summary>Builds the graph beside the spec at <paramref name="jsonPath"/>.</summary>
        public static void Build(string jsonPath)
        {
            var spec = EffectGraphSpec.Parse(File.ReadAllText(jsonPath), jsonPath);
            Build(spec, EffectGraphSpec.GraphPath(jsonPath), EffectGraphSpec.Stamp(jsonPath));
        }

        /// <summary>Builds <paramref name="spec"/> into the graph at <paramref name="graphPath"/>, stamped with <paramref name="stamp"/>.</summary>
        public static void Build(EffectGraphSpec spec, string graphPath, string stamp)
        {
            var graph = GraphModel.Open(graphPath);
            var inputs = new EffectGraphInputs(graph, spec.layers.Any(layer => layer.Streams));
            var updates = new Dictionary<string, object>();
            for (var i = 0; i < spec.layers.Count; i++)
            {
                var layer = spec.layers[i];
                var system = new EffectLayerSystem(graph, layer, i * 520f, inputs);
                updates[layer.name] = system.Write(layer.HasParent ? ParentUpdate(updates, layer) : null);
            }

            graph.Save(stamp);
        }

        private static object ParentUpdate(Dictionary<string, object> updates, EffectLayer layer) =>
            updates.TryGetValue(layer.parent, out var update)
                ? update
                : throw new InvalidOperationException($"HyperRTS: layer '{layer.name}' names parent '{layer.parent}', which no earlier layer is.");

        /// <summary>Builds every effect graph spec in the project, one graph at a time.</summary>
        public static void BuildAll()
        {
            foreach (var path in SpecPaths())
            {
                Build(path);
            }
        }

        /// <summary>Every <c>*.effect.json</c> in the project, the engine's included.</summary>
        public static IEnumerable<string> SpecPaths() =>
            AssetDatabase.FindAssets("effect t:TextAsset").Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(EffectGraphSpec.Extension, StringComparison.Ordinal)).Distinct().OrderBy(path => path);

        /// <summary>What is wrong with the graph of the spec at <paramref name="jsonPath"/>, null when it is current.</summary>
        public static string Problem(string jsonPath)
        {
            var graphPath = EffectGraphSpec.GraphPath(jsonPath);
            var importer = AssetImporter.GetAtPath(graphPath);
            if (importer == null)
            {
                return $"{jsonPath} has no graph {graphPath}; build it";
            }

            return importer.userData == EffectGraphSpec.Stamp(jsonPath) ? null : $"{graphPath} is older than {jsonPath}; rebuild it";
        }

        /// <summary>
        /// The compile errors of a built graph's compute shaders and of every output pass's default variant for
        /// Windows, which VFX Graph otherwise reports only once the graph first draws.
        /// </summary>
        public static IEnumerable<string> ShaderErrors(string graphPath)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(graphPath))
            {
                var messages = asset switch
                {
                    ComputeShader compute => ShaderUtil.GetComputeShaderMessages(compute),
                    Shader shader => PassMessages(shader),
                    _ => Enumerable.Empty<ShaderMessage>(),
                };
                foreach (var message in messages.Where(m => m.severity == ShaderCompilerMessageSeverity.Error))
                {
                    yield return $"{asset.name}: {message.message} ({message.file}:{message.line})";
                }
            }
        }

        private static IEnumerable<ShaderMessage> PassMessages(Shader shader)
        {
            var data = ShaderUtil.GetShaderData(shader);
            for (var s = 0; s < data.SerializedSubshaderCount; s++)
            {
                var subshader = data.GetSerializedSubshader(s);
                for (var p = 0; p < subshader.PassCount; p++)
                {
                    var pass = subshader.GetPass(p);
                    foreach (var stage in new[] { ShaderType.Vertex, ShaderType.Fragment }.Where(pass.HasShaderStage))
                    {
                        foreach (var message in pass.CompileVariant(stage, Array.Empty<string>(), ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64).Messages)
                        {
                            yield return message;
                        }
                    }
                }
            }
        }

        [MenuItem(BuildMenu)]
        private static void BuildSelected()
        {
            foreach (var path in SelectedSpecs())
            {
                Build(path);
            }
        }

        [MenuItem(BuildMenu, true)]
        private static bool CanBuildSelected() => SelectedSpecs().Any();

        [MenuItem(BuildAllMenu)]
        private static void BuildEvery() => BuildAll();

        private static IEnumerable<string> SelectedSpecs() =>
            Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).Where(path => path.EndsWith(EffectGraphSpec.Extension, StringComparison.Ordinal));
    }
}
