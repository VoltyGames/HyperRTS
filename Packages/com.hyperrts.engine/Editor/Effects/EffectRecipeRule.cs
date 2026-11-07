using System;
using HyperRTS.Editor.Validation;
using UnityEditor;

namespace HyperRTS.Editor.Effects
{
    /// <summary>
    /// Every effect recipe is named after its graph, has layers, no reference to a deleted asset, meshes its debris layers can use, and a graph
    /// built from its current state, so a broken recipe shows on the Validate page rather than when the next build throws.
    /// </summary>
    public sealed class EffectRecipeRule : IAssetRule
    {
        public void Check(AssetSet assets, ValidationIssues issues)
        {
            foreach (var path in EffectGraphBuilder.RecipePaths())
            {
                Check(path, issues);
            }
        }

        /// <summary>Checks the recipe at <paramref name="path"/>.</summary>
        public static void Check(string path, ValidationIssues issues)
        {
            var recipe = AssetDatabase.LoadAssetAtPath<EffectRecipe>(path);
            if (!EffectRecipe.IsRecipe(path))
            {
                issues.Error(recipe, $"Name the recipe <Graph>{EffectRecipe.Extension}, as its graph is named after it.");
                return;
            }

            if (recipe.layers.Count == 0)
            {
                issues.Error(recipe, "The recipe has no layers.");
                return;
            }

            foreach (var link in BrokenLinks.Find(recipe))
            {
                issues.Error(recipe, $"{link} names a deleted asset.");
            }

            foreach (var layer in recipe.layers)
            {
                if (layer.kind == EffectLayerKind.Debris)
                {
                    Report(recipe, () => layer.Meshes(), issues);
                }
            }

            var problem = EffectGraphBuilder.Problem(path);
            if (problem != null)
            {
                issues.Warn(recipe, problem, "Build", () => EffectGraphBuilder.Build(path));
            }
        }

        // The layer's own checks throw naming the layer and field, as a build would.
        private static void Report(EffectRecipe recipe, Action check, ValidationIssues issues)
        {
            try
            {
                check();
            }
            catch (InvalidOperationException exception)
            {
                issues.Error(recipe, exception.Message.Replace("HyperRTS: ", string.Empty));
            }
        }
    }
}
