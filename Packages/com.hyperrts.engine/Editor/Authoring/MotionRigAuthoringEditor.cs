using HyperRTS.Editor.Common;
using HyperRTS.Presentation.Animation;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.Common;
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
            var filled = rig.GetComponents<AuthoringBehaviour>();
            Undo.RecordObjects(filled, "Fill Motion Rig");
            rig.FillFromModel();
            foreach (var authoring in filled)
            {
                EditorUtility.SetDirty(authoring);
            }
        }
    }
}
