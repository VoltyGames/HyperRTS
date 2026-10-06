using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Production;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Move handle for the producer's spawn point.</summary>
    [CustomEditor(typeof(ProducerAuthoring))]
    [CanEditMultipleObjects]
    public class ProducerAuthoringEditor : AuthoringEditor
    {
        private void OnSceneGUI()
        {
            var producer = (ProducerAuthoring)target;
            var transform = producer.transform;
            var spawn = transform.TransformPoint(producer.spawnOffset);

            Handles.color = Color.green;
            Handles.DrawWireDisc(spawn, Vector3.up, 0.5f);
            Handles.Label(spawn + Vector3.forward * 0.7f, "Spawn");

            EditorGUI.BeginChangeCheck();
            var moved = Handles.PositionHandle(spawn, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUndo.Record(producer, "Move Spawn Point", () => producer.spawnOffset = transform.InverseTransformPoint(moved));
            }
        }
    }
}
