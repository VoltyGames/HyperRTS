using HyperRTS.Editor.Common;
using HyperRTS.Presentation.Animation;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.Common.Authoring;
using UnityEditor;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Motion rig inspector with a button that fills the slots, and the weapons' pivots, from the model's part names.</summary>
    [CustomEditor(typeof(MotionRigAuthoring))]
    public class MotionRigAuthoringEditor : AuthoringEditor
    {
        protected override void BuildFooter(VisualElement footer)
        {
            EditorAssets.Layout(typeof(MotionRigAuthoringEditor)).CloneTree(footer);
            footer.Require<Button>("fill").clicked += Fill;
        }

        private void Fill()
        {
            var rig = (MotionRigAuthoring)target;
            RecordThenFill(rig, rig.GetComponents<AuthoringBehaviour>(), 0);
        }

        // Nests one Record per component so each is captured before the fill changes it.
        private static void RecordThenFill(MotionRigAuthoring rig, AuthoringBehaviour[] filled, int index)
        {
            if (index == filled.Length)
            {
                rig.FillFromModel();
                return;
            }

            EditorUndo.Record(filled[index], "Fill Motion Rig", () => RecordThenFill(rig, filled, index + 1));
        }
    }
}
