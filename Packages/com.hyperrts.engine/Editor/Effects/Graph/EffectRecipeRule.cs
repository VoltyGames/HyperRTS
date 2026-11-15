using System.Linq;
using HyperRTS.Editor.Effects.Layer;
using HyperRTS.Editor.Validation;
using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Editor.Validation.Project;
using HyperRTS.Editor.Validation.Sets;
using UnityEditor;

namespace HyperRTS.Editor.Effects.Graph
{
    /// <summary>
    /// Every effect recipe is named after its graph, has layers, no reference to a deleted asset, layers a build accepts, and a graph
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

            foreach (var problem in recipe.layers.SelectMany(layer => layer.Problems()))
            {
                issues.Error(recipe, problem);
            }

            var stale = EffectGraphBuilder.Problem(path);
            if (stale != null)
            {
                issues.Warn(recipe, stale, "Build", () => EffectGraphBuilder.Build(path));
            }
        }
    }
}
