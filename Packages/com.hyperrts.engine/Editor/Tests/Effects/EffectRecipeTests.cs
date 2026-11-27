using System.Linq;
using HyperRTS.Editor.Effects.Graph;
using HyperRTS.Editor.Effects.Layer;
using HyperRTS.Editor.Validation.Issues;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Tests.Effects;

/// <summary>The recipe rule names a recipe without layers and a texture deleted from under a layer; the effect commands edit layer fields by path.</summary>
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
    public void SetEditsNestedRangeColourCurveAndBurstFields()
    {
        CreateLayerRecipe();
        var editor = new RecipeEditor(Recipe);

        var changes = editor.Set("Puff", RecipeEdit.ParseAll(
            "size.min=3;size*=2;color.min=1,0.5,0,0.25;alphaOverLife=0:0.9,1:0;bursts=0:1-2,0.7:3-3;flipbook.columns=2;kind=glow"));
        var layer = editor.Layers("Puff").Single();

        Assert.AreEqual(7, changes.Count);
        Assert.AreEqual(6f, layer.size.min);
        Assert.AreEqual(4f, layer.size.max);
        Assert.AreEqual(new Color(1f, 0.5f, 0f, 0.25f), layer.color.min);
        Assert.AreEqual(0.9f, layer.alphaOverLife.Evaluate(0f), 1e-4f);
        Assert.AreEqual(0.45f, layer.alphaOverLife.Evaluate(0.5f), 1e-4f);
        Assert.AreEqual(2, layer.bursts.Count);
        Assert.AreEqual(0.7f, layer.bursts[1].delay);
        Assert.AreEqual(2, layer.flipbook.columns);
        Assert.AreEqual(EffectLayerKind.Glow, layer.kind);
    }

    [Test]
    public void SetNamesAnUnknownLayerAndAnUnknownField()
    {
        CreateLayerRecipe();
        var editor = new RecipeEditor(Recipe);

        StringAssert.Contains("no layer 'Nope'", Assert.Throws<System.ArgumentException>(() => editor.Set("Nope", RecipeEdit.ParseAll("size.min=1"))).Message);
        StringAssert.Contains("no field 'sizee'", Assert.Throws<System.ArgumentException>(() => editor.Set("Puff", RecipeEdit.ParseAll("sizee=1"))).Message);
        Assert.Throws<System.ArgumentException>(() => RecipeEdit.ParseAll("=3"));
    }

    private static void CreateLayerRecipe()
    {
        var recipe = ScriptableObject.CreateInstance<EffectRecipe>();
        recipe.layers.Add(new EffectLayer
        {
            name = "Puff", kind = EffectLayerKind.Puff, capacity = 8, size = new FloatRange(1f, 2f),
        });
        AssetDatabase.CreateAsset(recipe, Recipe);
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
