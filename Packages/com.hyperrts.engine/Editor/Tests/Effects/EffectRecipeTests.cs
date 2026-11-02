using System.Linq;
using HyperRTS.Editor.Effects;
using HyperRTS.Editor.Validation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Tests.Effects
{
    /// <summary>The recipe rule names a recipe without layers and a texture deleted from under a layer.</summary>
    public class EffectRecipeTests
    {
        private const string Folder = "Assets/_EffectRecipeTests";
        private const string Recipe = Folder + "/Test" + EffectRecipe.Extension;
        private const string Texture = Folder + "/T_Gone.asset";

        [SetUp]
        public void CreateFolder() => AssetDatabase.CreateFolder("Assets", "_EffectRecipeTests");

        [TearDown]
        public void DeleteFolder() => AssetDatabase.DeleteAsset(Folder);

        [Test]
        public void ARecipeWithoutLayers_IsAnError()
        {
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<EffectRecipe>(), Recipe);
            var issues = new ValidationIssues();

            EffectRecipeRule.Check(Recipe, issues);

            StringAssert.Contains("no layers", issues.Single(issue => issue.Severity == MessageType.Error).Message);
        }

        [Test]
        public void ADeletedTexture_IsReportedByField()
        {
            AssetDatabase.CreateAsset(new Texture2D(4, 4), Texture);
            var recipe = ScriptableObject.CreateInstance<EffectRecipe>();
            recipe.layers.Add(new EffectLayer
            {
                name = "Puff", kind = EffectLayerKind.Puff, capacity = 8, texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Texture),
            });
            AssetDatabase.CreateAsset(recipe, Recipe);
            AssetDatabase.SaveAssets();
            AssetDatabase.DeleteAsset(Texture);
            var issues = new ValidationIssues();

            EffectRecipeRule.Check(Recipe, issues);

            var error = issues.First(issue => issue.Severity == MessageType.Error);
            StringAssert.Contains("layers.Array.data[0].texture names a deleted asset", error.Message);
            Assert.AreEqual(Recipe, AssetDatabase.GetAssetPath(error.Context));
        }
    }
}
