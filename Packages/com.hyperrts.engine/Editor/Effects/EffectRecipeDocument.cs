using System;
using HyperRTS.Editor.Common;
using UnityEngine;

namespace HyperRTS.Editor.Effects
{
    /// <summary>An effect recipe open in its Inspector, saved at once through <see cref="EffectGraphSpec.WriteOver"/>.</summary>
    public sealed class EffectRecipeDocument : JsonDocument
    {
        /// <summary>The serialized field holding the recipe, the root of every property path (<c>recipe.layers</c>).</summary>
        public const string RecipeField = nameof(recipe);

        [SerializeField] private EffectGraphSpec recipe = new();

        public EffectGraphSpec Recipe => recipe;

        /// <summary>The document for <paramref name="recipePath"/>; the caller destroys it when done.</summary>
        public static EffectRecipeDocument Open(string recipePath)
        {
            if (!EffectGraphSpec.IsRecipe(recipePath))
            {
                throw new ArgumentException($"'{recipePath}' is not an effect recipe ({EffectGraphSpec.Extension}).");
            }

            return Open<EffectRecipeDocument>(recipePath);
        }

        protected override void Read(string json) => recipe = EffectGraphSpec.Parse(json, Path);

        protected override string Write() => recipe.Write();

        protected override string WriteOver(string fileJson) => recipe.WriteOver(fileJson, Path);

        protected override string Canonical(string fileJson) => EffectGraphSpec.Parse(fileJson, Path).Write();
    }
}
