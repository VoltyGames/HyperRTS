using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Effects.Preview;
using HyperRTS.Editor.Hub;
using HyperRTS.Presentation.HUD;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Effects
{
    /// <summary>An effect recipe's Inspector: a Build button and whether its graph is current, above its layers.</summary>
    [CustomEditor(typeof(EffectRecipe))]
    public sealed class EffectRecipeInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var path = AssetDatabase.GetAssetPath(target);
            var problem = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            root.Add(new Button(() =>
            {
                EffectGraphBuilder.Build(path);
                ShowProblem(problem, path);
            }) { text = "Build graph" });
            root.Add(new Button(() => Preview(path, problem)) { text = "Preview" });
            root.Add(problem);
            ShowProblem(problem, path);

            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            root.TrackSerializedObjectValue(serializedObject, _ => ShowProblem(problem, path));
            return root;
        }

        // The effects page opens on the first effect rig whose graphs this recipe builds.
        private static void Preview(string path, HelpBox box)
        {
            var rig = EffectRigEntry.Find().FirstOrDefault(entry => entry.Recipes.Contains(path));
            if (rig == null)
            {
                box.text = "No effect rig uses this recipe's graph.";
                box.SetHidden(false);
                return;
            }

            EditorState.instance.effectPreview.lastEffect = rig.Path;
            EditorState.instance.Save();
            ToolsWindow.Show<EffectsPage>();
        }

        private static void ShowProblem(HelpBox box, string path)
        {
            var problem = EffectGraphBuilder.Problem(path);
            box.text = problem ?? string.Empty;
            box.SetHidden(problem == null);
        }
    }
}
