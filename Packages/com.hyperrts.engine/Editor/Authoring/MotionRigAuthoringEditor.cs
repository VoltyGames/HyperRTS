using HyperRTS.Editor.Common;
using HyperRTS.Presentation.Animation;
using UnityEditor;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Motion rig inspector with a button that fills the slots from the model's part names.</summary>
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
            Undo.RecordObject(rig, "Fill Motion Rig");
            rig.FillFromModel();
            EditorUtility.SetDirty(rig);
        }
    }
}
