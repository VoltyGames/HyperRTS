using System.IO;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Effects;
using HyperRTS.Editor.Validation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Tests.Effects
{
    /// <summary>
    /// A recipe saves like a hand edit, every recipe in the project rewrites unchanged, and the rule names a broken
    /// asset reference by layer and field.
    /// </summary>
    public class EffectRecipeTests
    {
        private const string Folder = "Assets/_EffectRecipeTests";
        private const string Recipe = Folder + "/Test.effect.json";
        private const string Texture = "Packages/com.hyperrts.engine/Presentation/Environment/EdgeNoise.png";

        private const string Text = "{\"layers\": [{\"name\": \"Puff\", \"kind\": \"puff\", \"capacity\": 8, \"shape\": \"sphere\", " +
            "\"texture\": \"" + Texture + "\", \"alphaOverLife\": [{\"time\": 0, \"value\": 1}, {\"time\": 1, \"value\": 0}]}]}";

        [TearDown]
        public void DeleteFolder() => AssetDatabase.DeleteAsset(Folder);

        [Test]
        public void AChangedFieldIsTheOnlyChange()
        {
            var file = JsonFormat.Format(Text);
            var recipe = EffectGraphSpec.Parse(file, "test");
            recipe.layers[0].capacity = 16;

            Assert.AreEqual(JsonFormat.Format(Text.Replace("\"capacity\": 8", "\"capacity\": 16")), recipe.WriteOver(file, "test"));
        }

        [Test]
        public void AnAddedLayerLeavesOutItsDefaults()
        {
            var file = JsonFormat.Format(Text);
            var recipe = EffectGraphSpec.Parse(file, "test");
            recipe.layers.Add(new EffectLayer { name = "Glow", kind = "glow" });

            var written = recipe.WriteOver(file, "test");

            StringAssert.Contains("\"Glow\"", written);
            Assert.AreEqual(1, written.Split("\"capacity\"").Length - 1, "The added layer wrote its default capacity.");
            Assert.AreEqual(recipe.Write(), EffectGraphSpec.Parse(written, "test").Write(), "The added layer lost a value.");
        }

        [Test]
        public void EveryProjectRecipeRewritesUnchanged()
        {
            foreach (var path in EffectGraphBuilder.SpecPaths())
            {
                var file = File.ReadAllText(path).Replace("\r\n", "\n");
                var recipe = EffectGraphSpec.Parse(file, path);
                Assert.AreEqual(file.TrimEnd(), recipe.WriteOver(file, path).Replace("\r\n", "\n").TrimEnd(), $"{path} changes when saved untouched.");
            }
        }

        [Test]
        public void AMissingTextureIsReportedByLayerAndField()
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Recipe, JsonFormat.Format(Text.Replace(Texture, "Assets/Nowhere/T_Gone.png")));
            AssetDatabase.ImportAsset(Recipe);
            var issues = new ValidationIssues();

            EffectRecipeRule.Check(Recipe, issues);

            var error = issues.Single(issue => issue.Severity == MessageType.Error);
            StringAssert.Contains("layer 'Puff' texture 'Assets/Nowhere/T_Gone.png'", error.Message);
            Assert.AreEqual(Recipe, AssetDatabase.GetAssetPath(error.Context));
        }

        [Test]
        public void ADocumentEditSavesTheRecipe()
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Recipe, JsonFormat.Format(Text));
            AssetDatabase.ImportAsset(Recipe);
            var document = EffectRecipeDocument.Open(Recipe);
            try
            {
                document.Serialized.FindProperty(EffectRecipeDocument.RecipeField + ".layers.Array.data[0].capacity").intValue = 24;
                document.Apply("Capacity");

                Assert.AreEqual(JsonFormat.Format(Text.Replace("\"capacity\": 8", "\"capacity\": 24")), File.ReadAllText(Recipe));
            }
            finally
            {
                Object.DestroyImmediate(document);
            }
        }
    }
}
