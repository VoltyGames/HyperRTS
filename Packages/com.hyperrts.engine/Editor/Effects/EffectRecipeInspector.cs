using System;
using HyperRTS.Presentation.HUD;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Effects
{
    /// <summary>
    /// The Inspector of a selected <c>.effect.json</c>: its layers as property fields, with curves, HDR colour pickers
    /// and asset pickers, each change saved to the recipe at once. Any other text asset keeps Unity's own inspector.
    /// </summary>
    [CustomEditor(typeof(TextAsset))]
    public sealed class EffectRecipeInspector : UnityEditor.Editor
    {
        private const string LayersPath = EffectRecipeDocument.RecipeField + "." + nameof(EffectGraphSpec.layers);

        // Unity's text asset inspector is internal, so it is found by name.
        private static readonly Type TextInspector = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.TextAssetInspector");

        private EffectRecipeDocument _document;
        private UnityEditor.Editor _text;

        private string RecipePath => targets.Length == 1 ? AssetDatabase.GetAssetPath(target) : null;

        public override VisualElement CreateInspectorGUI()
        {
            var path = RecipePath;
            if (path == null || !EffectGraphSpec.IsRecipe(path))
            {
                return null;
            }

            var root = new VisualElement();
            try
            {
                _document = EffectRecipeDocument.Open(path);
            }
            catch (Exception exception) when (exception is InvalidOperationException or FormatException)
            {
                root.Add(new HelpBox(exception.Message, HelpBoxMessageType.Error));
                return root;
            }

            var problem = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            var build = new Button(() =>
            {
                EffectGraphBuilder.Build(path);
                ShowProblem(problem, path);
            }) { text = "Build graph" };
            root.Add(build);
            root.Add(problem);
            ShowProblem(problem, path);

            var layers = new PropertyField(_document.Serialized.FindProperty(LayersPath));
            root.Add(layers);
            root.Bind(_document.Serialized);
            // Fires for every change to the recipe; a reload or undo has already saved, so committing it writes nothing.
            root.TrackSerializedObjectValue(_document.Serialized, _ =>
            {
                _document.Commit();
                ShowProblem(problem, path);
            });
            return root;
        }

        public override void OnInspectorGUI()
        {
            if (_text == null && TextInspector != null)
            {
                _text = CreateEditor(targets, TextInspector);
            }

            if (_text != null)
            {
                _text.OnInspectorGUI();
            }
        }

        private void OnDisable()
        {
            if (_document != null)
            {
                DestroyImmediate(_document);
            }

            if (_text != null)
            {
                DestroyImmediate(_text);
            }
        }

        private static void ShowProblem(HelpBox box, string path)
        {
            var problem = EffectGraphBuilder.Problem(path);
            box.text = problem ?? string.Empty;
            box.SetHidden(problem == null);
        }
    }
}
