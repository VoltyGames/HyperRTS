using HyperRTS.Editor.Common;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.Combat;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Topple inspector with a button that measures trunk and lie from the standing and remains models.</summary>
    [CustomEditor(typeof(ToppleAuthoring))]
    public class ToppleAuthoringEditor : AuthoringEditor
    {
        private ObjectField _trunkMaterial;
        private HelpBox _error;

        protected override void BuildFooter(VisualElement footer)
        {
            EditorAssets.Layout(typeof(ToppleAuthoringEditor)).CloneTree(footer);
            _trunkMaterial = footer.Require<ObjectField>("trunk-material");
            _trunkMaterial.objectType = typeof(Material);
            var measure = footer.Require<Button>("measure");
            measure.clicked += Measure;
            measure.SetEnabled(false);
            _trunkMaterial.RegisterValueChangedCallback(change => measure.SetEnabled(change.newValue != null));
            _error = footer.Require<HelpBox>("error");
        }

        private void Measure()
        {
            var topple = (ToppleAuthoring)target;
            _error.SetHidden(true);
            try
            {
                Undo.RecordObject(topple, "Measure Topple");
                ToppleMeasure.Measure(topple, (Material)_trunkMaterial.value);
                EditorUtility.SetDirty(topple);
            }
            catch (System.InvalidOperationException problem)
            {
                _error.text = problem.Message;
                _error.SetHidden(false);
            }
        }
    }
}
