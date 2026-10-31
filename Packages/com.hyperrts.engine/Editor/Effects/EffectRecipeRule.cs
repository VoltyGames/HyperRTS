using System;
using System.IO;
using HyperRTS.Editor.Validation;
using HyperRTS.Editor.Validation.Rules;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Effects
{
    /// <summary>
    /// Every effect recipe reads, every asset it names loads as the type its layer needs, and its graph was built from
    /// its current text, so a moved texture shows on the Validate page rather than when the next build throws.
    /// </summary>
    public sealed class EffectRecipeRule : IAssetRule
    {
        public void Check(AssetSet assets, ValidationIssues issues)
        {
            foreach (var path in EffectGraphBuilder.SpecPaths())
            {
                Check(path, issues);
            }
        }

        /// <summary>Checks the recipe at <paramref name="path"/>.</summary>
        public static void Check(string path, ValidationIssues issues)
        {
            var recipe = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            EffectGraphSpec spec;
            try
            {
                spec = EffectGraphSpec.Parse(File.ReadAllText(path), path);
            }
            catch (Exception exception) when (exception is InvalidOperationException or FormatException)
            {
                issues.Error(recipe, Plain(exception));
                return;
            }

            foreach (var layer in spec.layers)
            {
                CheckTexture(recipe, layer, layer.texture, nameof(EffectLayer.texture), issues);
                CheckTexture(recipe, layer, layer.negativeTexture, nameof(EffectLayer.negativeTexture), issues);
                CheckTexture(recipe, layer, layer.motionTexture, nameof(EffectLayer.motionTexture), issues);
                CheckTexture(recipe, layer, layer.emissionRamp, nameof(EffectLayer.emissionRamp), issues);
                if (!string.IsNullOrEmpty(layer.mesh))
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

        private static void CheckTexture(TextAsset recipe, EffectLayer layer, string reference, string field, ValidationIssues issues)
        {
            if (!string.IsNullOrEmpty(reference))
            {
                Report(recipe, () => layer.Load<Texture2D>(reference, field), issues);
            }
        }

        private static string Plain(Exception exception) => exception.Message.Replace("HyperRTS: ", string.Empty);

        // The layer's own loaders throw naming the layer, field and reference, as a build would.
        private static void Report(TextAsset recipe, Action load, ValidationIssues issues)
        {
            try
            {
                load();
            }
            catch (InvalidOperationException exception)
            {
                issues.Error(recipe, Plain(exception));
            }
        }
    }
}
