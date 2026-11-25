using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Effects.Layer;
using HyperRTS.Presentation.Effects;
using HyperRTS.Presentation.Effects.Rig;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.VFX;

namespace HyperRTS.Editor.Effects.Graph
{
    /// <summary>
    /// Builds a rig's VFX Graph from its <see cref="EffectRecipe"/>, one system per layer, stamped with the recipe so a
    /// stale graph is found. Also finds recipes and reports a graph's state and shader errors.
    /// </summary>
    public static class EffectGraphBuilder
    {
        /// <summary>Builds the graph beside the recipe at <paramref name="recipePath"/>, saving the recipe first so its stamp matches what was built.</summary>
        public static void Build(string recipePath)
        {
            var recipe = Load(recipePath);
            AssetDatabase.SaveAssetIfDirty(recipe);
            Build(recipe, EffectRecipe.GraphPath(recipePath), EffectRecipe.Stamp(recipePath));
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
                var problems = layer.Problems();
                if (problems.Count > 0)
                {
                    throw new InvalidOperationException("HyperRTS: " + string.Join(" ", problems));
                }

                var particles = new EffectLayerParticles(graph, layer, i * 520f, inputs);
                updates[layer.name] = particles.Write(layer.HasParent ? ParentUpdate(updates, layer) : null);
            }

            graph.Save(stamp);
        }

        private static object ParentUpdate(Dictionary<string, object> updates, EffectLayer layer) =>
            updates.TryGetValue(layer.parent, out var update)
                ? update
                : throw new InvalidOperationException($"HyperRTS: layer '{layer.name}' names parent '{layer.parent}', which no earlier layer is.");

        /// <summary>Builds the graph of each recipe in <paramref name="recipePaths"/>, one at a time.</summary>
        public static void BuildAll(IEnumerable<string> recipePaths)
        {
            foreach (var path in recipePaths)
            {
                Build(path);
            }
        }

        /// <summary>Every effect recipe in the project, the engine's included.</summary>
        public static IEnumerable<string> RecipePaths() =>
            AssetDatabase.FindAssets("t:" + nameof(EffectRecipe)).Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path);

        /// <summary>The recipe for a graph name (<c>FX_Smoke_01</c>) or recipe path; throws naming the graph when there is none.</summary>
        public static string FindRecipe(string graph)
        {
            var normalized = graph.Replace('\\', '/');
            return RecipePaths().FirstOrDefault(path => path == normalized || Path.GetFileName(path) == normalized + EffectRecipe.Extension)
                ?? throw new ArgumentException($"HyperRTS: no effect recipe is named '{graph}'.");
        }

        /// <summary>Whether the graph of the recipe at <paramref name="recipePath"/> exists and matches the recipe.</summary>
        public static EffectGraphState State(string recipePath)
        {
            var importer = AssetImporter.GetAtPath(EffectRecipe.GraphPath(recipePath));
            if (importer == null)
            {
                return EffectGraphState.Missing;
            }

            return importer.userData == EffectRecipe.Stamp(recipePath) ? EffectGraphState.Built : EffectGraphState.OutOfDate;
        }

        /// <summary>What is wrong with the graph of the recipe at <paramref name="recipePath"/>, null when it is current.</summary>
        public static string Problem(string recipePath)
        {
            var graphPath = EffectRecipe.GraphPath(recipePath);
            return State(recipePath) switch
            {
                EffectGraphState.Missing => $"{recipePath} has no graph {graphPath}; build it",
                EffectGraphState.OutOfDate => $"{graphPath} is older than {recipePath}; rebuild it",
                _ => null,
            };
        }

        /// <summary>The recipes of the graphs on a rig prefab and its variants, each once; graphs with no recipe beside them are left out.</summary>
        public static List<string> RecipesFor(GameObject rig)
        {
            var parts = new List<GameObject> { rig };
            if (rig.TryGetComponent<EffectRig>(out var root))
            {
                parts.AddRange(root.variants.Where(variant => variant != null));
            }

            return parts.SelectMany(part => part.GetComponentsInChildren<VisualEffect>(true))
                .Select(effect => AssetDatabase.GetAssetPath(effect.visualEffectAsset))
                .Where(graph => graph.Length > 0)
                .Select(graph => Path.ChangeExtension(graph, null) + EffectRecipe.Extension)
                .Where(File.Exists)
                .Distinct()
                .ToList();
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
    }
}
