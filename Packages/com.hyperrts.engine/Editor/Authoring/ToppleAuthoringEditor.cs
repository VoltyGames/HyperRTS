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

        protected override void BuildFooter(VisualElement root)
        {
            _trunkMaterial = new ObjectField("Trunk material")
            {
                objectType = typeof(Material),
                tooltip = "The material of the trunk faces in the standing model's LOD0.",
            };
            var measure = new Button(Measure) { text = "Measure from model" };
            measure.SetEnabled(false);
            _trunkMaterial.RegisterValueChangedCallback(change => measure.SetEnabled(change.newValue != null));
            _error = new HelpBox(string.Empty, HelpBoxMessageType.Error);
            _error.style.display = DisplayStyle.None;
            root.Add(_trunkMaterial);
            root.Add(measure);
            root.Add(_error);
        }

        private void Measure()
        {
            var topple = (ToppleAuthoring)target;
            _error.style.display = DisplayStyle.None;
            try
            {
                Undo.RecordObject(topple, "Measure Topple");
                ToppleMeasure.Measure(topple, (Material)_trunkMaterial.value);
                EditorUtility.SetDirty(topple);
            }
            catch (System.InvalidOperationException problem)
            {
                _error.text = problem.Message;
                _error.style.display = DisplayStyle.Flex;
            }
        }
    }
}
