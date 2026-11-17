using System;
using System.Collections.Generic;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Icons;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>A window of Play-mode tools: foldout sections that remember their state and show a glyph, docking beside the Game view.</summary>
    public abstract class PlayModeWindow : EditorWindow
    {
        // Glyph names from Icons/Glyphs.uss, by foldout name.
        [NoAutoStaticsCleanup]
        private static readonly Dictionary<string, string> Glyphs = new()
        {
            ["player-group"] = "library",
            ["spawn-group"] = "plus",
            ["combat-group"] = "scan",
            ["strikes-group"] = "wand-sparkles",
            ["world-group"] = "cloud-fog",
            ["layers-group"] = "layers",
            ["ai-group"] = "scan-eye",
        };

        public void CreateGUI()
        {
            var root = rootVisualElement;
            EditorAssets.Layout(GetType()).CloneTree(root);
            EditorState.Bind(root);
            root.Query<Foldout>().ForEach(RememberOpen);
            root.Query<Foldout>().ForEach(ShowGlyph);
            Build(root);
        }

        /// <summary>Wires the cloned layout; its sections are already remembered and glyphed.</summary>
        protected abstract void Build(VisualElement root);

        /// <summary>Opens the window floating on its own; the user may dock it anywhere.</summary>
        protected static T Open<T>(string title) where T : PlayModeWindow => GetWindow<T>(true, title, true);

        // The glyph sits in the header between the arrow and the title, like the other tool windows' headers.
        private static void ShowGlyph(Foldout foldout)
        {
            if (Glyphs.TryGetValue(foldout.name, out var glyph))
            {
                foldout.Q<VisualElement>(className: Foldout.inputUssClassName).Insert(1, GlyphElements.CreateGlyph(glyph, PickingMode.Ignore));
            }
        }

        private void RememberOpen(Foldout foldout)
        {
            var key = $"{GetType().Name}/{foldout.name}";
            foldout.SetValueWithoutNotify(!EditorState.instance.IsGroupClosed(key));
            foldout.RegisterValueChangedCallback(change =>
            {
                // Toggles inside the foldout send their own change events up through it.
                if (change.target == foldout)
                {
                    EditorState.instance.SetGroupClosed(key, !change.newValue);
                }
            });
        }
    }
}
