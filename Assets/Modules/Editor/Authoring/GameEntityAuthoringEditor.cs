using HyperRTS.Simulation.Common;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Default inspector plus setup warnings for units and buildings.</summary>
    [CustomEditor(typeof(GameEntityAuthoring), true)]
    [CanEditMultipleObjects]
    public class GameEntityAuthoringEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var authoring = (GameEntityAuthoring)target;
            if (authoring.GetComponentInChildren<Collider>() == null)
            {
                EditorGUILayout.HelpBox("No collider: this can't be clicked, selected or right-click targeted.",
                    MessageType.Warning);
            }

            if (authoring.GetComponentInChildren<Renderer>() == null)
            {
                EditorGUILayout.HelpBox("No renderer: this will be invisible in the Game view.", MessageType.Info);
            }

            if (authoring.cost.Exists(quantity => quantity.type == null))
            {
                EditorGUILayout.HelpBox("A cost entry has no resource type and will be ignored.", MessageType.Warning);
            }

            if (!authoring.gameObject.scene.IsValid() || authoring.gameObject.scene.isSubScene)
            {
                return;
            }

            EditorGUILayout.HelpBox("Entities only bake inside a SubScene. Move this into the scene's SubScene.",
                MessageType.Warning);
        }
    }
}
