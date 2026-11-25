using System;
using UnityEditor;

namespace HyperRTS.Editor.Validation.Project
{
    /// <summary>Reads and writes the names of rendering layers in Tags and Layers.</summary>
    public static class RenderingLayerNames
    {
        private const string TagManagerPath = "ProjectSettings/TagManager.asset";

        /// <summary>The name of a rendering layer; empty when unnamed.</summary>
        public static string Name(int index)
        {
            var layers = Property(TagManager(), "m_RenderingLayers");
            if (index >= layers.arraySize)
            {
                return string.Empty;
            }

            return layers.GetArrayElementAtIndex(index).stringValue;
        }

        public static void SetName(int index, string name)
        {
            var tagManager = TagManager();
            var layers = Property(tagManager, "m_RenderingLayers");
            // Growing an array copies its last name into the new slots, so blank them.
            for (var i = layers.arraySize; i <= index; i++)
            {
                layers.arraySize = i + 1;
                layers.GetArrayElementAtIndex(i).stringValue = string.Empty;
            }

            layers.GetArrayElementAtIndex(index).stringValue = name;
            tagManager.ApplyModifiedProperties();
        }

        private static SerializedObject TagManager() =>
            new(AssetDatabase.LoadAllAssetsAtPath(TagManagerPath)[0]);

        private static SerializedProperty Property(SerializedObject settings, string path) =>
            settings.FindProperty(path)
            ?? throw new InvalidOperationException($"HyperRTS: {settings.targetObject.name} has no setting '{path}'.");
    }
}
