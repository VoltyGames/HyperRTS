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
    /// Writes a rig's VFX Graph from its <see cref="EffectRecipe"/>: the Burst event, the Density property, the
    /// stream events and properties when a layer has a rate, and one system per layer, each placed where its spawn
    /// came from, thinned by Density and shaded by the fog of war. The graph is generated output, stamped with its
    /// recipe so a stale one is found.
    /// </summary>
    public static class EffectGraphBuilder
    {
        private const string BuildMenu = "Assets/HyperRTS/Build Effect Graph";
        private const string BuildAllMenu = "Assets/HyperRTS/Build All Effect Graphs";

        /// <summary>Builds the graph beside the recipe at <paramref name="recipePath"/>.</summary>
        public static void Build(string recipePath)
        {
            Build(Load(recipePath), EffectRecipe.GraphPath(recipePath), EffectRecipe.Stamp(recipePath));
        }

        /// <summary>Builds <paramref name="recipe"/> into the graph at <paramref name="graphPath"/>, stamped with <paramref name="stamp"/>.</summary>
        public static void Build(EffectRecipe recipe, string graphPath, string stamp)
        {
            if (recipe.layers.Count == 0)
            {
                throw new InvalidOperationException($"HyperRTS: effect recipe '{recipe.name}' has no layers.");
            }

            var graph = GraphModel.Open(graphPath);
            var inputs = new EffectGraphInputs(graph, recipe.layers.Any(layer => layer.Streams));
            var updates = new Dictionary<string, object>();
            for (var i = 0; i < recipe.layers.Count; i++)
            {
                var layer = recipe.layers[i];
                var system = new EffectLayerSystem(graph, layer, i * 520f, inputs);
                updates[layer.name] = system.Write(layer.HasParent ? ParentUpdate(updates, layer) : null);
            }

            graph.Save(stamp);
        }

        private static object ParentUpdate(Dictionary<string, object> updates, EffectLayer layer) =>
            updates.TryGetValue(layer.parent, out var update)
                ? update
                : throw new InvalidOperationException($"HyperRTS: layer '{layer.name}' names parent '{layer.parent}', which no earlier layer is.");

        /// <summary>Builds every effect recipe in the project, one graph at a time.</summary>
        public static void BuildAll()
        {
            foreach (var path in RecipePaths())
            {
                Build(path);
            }
        }

        /// <summary>Every effect recipe in the project, the engine's included.</summary>
        public static IEnumerable<string> RecipePaths() =>
            AssetDatabase.FindAssets("t:" + nameof(EffectRecipe)).Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(path => path);

        /// <summary>The recipe for a graph name (<c>FX_Smoke_01</c>) or recipe path; throws naming the graph when there is none.</summary>
        public static string FindRecipe(string graph)
        {
            var normalized = graph.Replace('\\', '/');
            return RecipePaths().FirstOrDefault(path => path == normalized || Path.GetFileName(path) == normalized + EffectRecipe.Extension)
                ?? throw new ArgumentException($"HyperRTS: no effect recipe is named '{graph}'.");
        }

        /// <summary>What is wrong with the graph of the recipe at <paramref name="recipePath"/>, null when it is current.</summary>
        public static string Problem(string recipePath)
        {
            var graphPath = EffectRecipe.GraphPath(recipePath);
            var importer = AssetImporter.GetAtPath(graphPath);
            if (importer == null)
            {
                return $"{recipePath} has no graph {graphPath}; build it";
            }

            return importer.userData == EffectRecipe.Stamp(recipePath) ? null : $"{graphPath} is older than {recipePath}; rebuild it";
        }

        private static EffectRecipe Load(string recipePath) =>
            AssetDatabase.LoadAssetAtPath<EffectRecipe>(recipePath)
            ?? throw new ArgumentException($"HyperRTS: {recipePath} is no effect recipe.");

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
            foreach (var path in SelectedRecipes())
            {
                Build(path);
            }
        }

        [MenuItem(BuildMenu, true)]
        private static bool CanBuildSelected() => SelectedRecipes().Any();

        [MenuItem(BuildAllMenu)]
        private static void BuildEvery() => BuildAll();

        private static IEnumerable<string> SelectedRecipes() =>
            Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).Where(EffectRecipe.IsRecipe);
    }
}
