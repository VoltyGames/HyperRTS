using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Production;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Ground-plane drag handle for the producer's spawn point.</summary>
    [CustomEditor(typeof(ProducerAuthoring))]
    [CanEditMultipleObjects]
    public class ProducerAuthoringEditor : AuthoringEditor
    {
        private void OnSceneGUI()
        {
            var producer = (ProducerAuthoring)target;
            var transform = producer.transform;
            var spawn = transform.TransformPoint(producer.spawnOffset);

            EditorGUI.BeginChangeCheck();
            var moved = GroundHandles.Point(spawn, Color.green, "Spawn");
            if (EditorGUI.EndChangeCheck())
            {
                EditorUndo.Record(producer, "Move Spawn Point", () => producer.spawnOffset = transform.InverseTransformPoint(moved));
            }
        }
    }
}
