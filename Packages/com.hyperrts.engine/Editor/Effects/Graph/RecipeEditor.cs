using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Effects.Layer;
using UnityEditor;

namespace HyperRTS.Editor.Effects.Graph;

/// <summary>Reads and edits the layers of one effect recipe by layer name and serialized field path, for the effect commands.</summary>
internal sealed class RecipeEditor
{
    private readonly EffectRecipe _recipe;
    private readonly SerializedObject _serialized;

    public RecipeEditor(string recipe)
    {
        Path = EffectGraphBuilder.FindRecipe(recipe);
        _recipe = AssetDatabase.LoadAssetAtPath<EffectRecipe>(Path)
            .OrThrow($"HyperRTS: {Path} is no effect recipe.");
        _serialized = new SerializedObject(_recipe);
    }

    public string Path { get; }

    /// <summary>The layer named <paramref name="name"/>, or every layer when it is empty; throws naming an unknown layer.</summary>
    public IEnumerable<EffectLayer> Layers(string name) =>
        string.IsNullOrEmpty(name) ? _recipe.layers : new[] { Layer(name) };

    public int LayerIndex(EffectLayer layer) => _recipe.layers.IndexOf(layer);

    /// <summary>Applies the edits to the named layer and returns each change as <c>layer.field: before -> after</c>; the asset is dirty after.</summary>
    public List<string> Set(string layer, IEnumerable<RecipeEdit> edits)
    {
        var element = _serialized.FindProperty("layers").GetArrayElementAtIndex(LayerIndex(Layer(layer)));
        var changes = new List<string>();
        foreach (var edit in edits)
        {
            var property = element.FindPropertyRelative(edit.Field)
                ?? throw new ArgumentException($"HyperRTS: layer '{layer}' has no field '{edit.Field}'.");
            var text = new PropertyText(property);
            var before = text.Read();
            if (edit.Scales)
            {
                text.Scale(PropertyText.ParseFloat(edit.Value, edit.Field));
            }
            else
            {
                text.Write(edit.Value);
            }

            changes.Add($"{layer}.{edit.Field}: {before} -> {text.Read()}");
        }

        _serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(_recipe);
        return changes;
    }

    public void Save() => AssetDatabase.SaveAssetIfDirty(_recipe);

    private EffectLayer Layer(string name) =>
        _recipe.layers.FirstOrDefault(layer => layer.name == name)
        ?? throw new ArgumentException($"HyperRTS: {Path} has no layer '{name}'; its layers are {string.Join(", ", _recipe.layers.Select(layer => layer.name))}.");
}
