using System;
using HyperRTS.Presentation.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Editor.Validation
{
    /// <summary>
    /// The project settings unit silhouettes need: <see cref="UnitSilhouetteFeature"/> on the active URP renderers and
    /// the <see cref="UnitRenderingLayer"/> name. Checks for the validator and their quick fixes.
    /// </summary>
    public static class UnitSilhouetteSettings
    {
        private const string TagManagerPath = "ProjectSettings/TagManager.asset";
        private const string FeatureName = "Unit Silhouettes";

        public static bool HasFeature(UniversalRendererData data) =>
            RendererFeatures.Find<UnitSilhouetteFeature>(data) != null;

        public static void AddFeature(UniversalRendererData data) =>
            RendererFeatures.Add(data, ScriptableObject.CreateInstance<UnitSilhouetteFeature>(), FeatureName);

        /// <summary>The name of the units' rendering layer in Tags and Layers; empty when unnamed.</summary>
        public static string LayerName()
        {
            var layers = Property(TagManager(), "m_RenderingLayers");
            if (UnitRenderingLayer.Index >= layers.arraySize)
            {
                return string.Empty;
            }

            return layers.GetArrayElementAtIndex(UnitRenderingLayer.Index).stringValue;
        }

        public static void NameLayer()
        {
            var tagManager = TagManager();
            var layers = Property(tagManager, "m_RenderingLayers");
            // Growing an array copies its last name into the new slots, so blank them.
            for (var i = layers.arraySize; i <= UnitRenderingLayer.Index; i++)
            {
                layers.arraySize = i + 1;
                layers.GetArrayElementAtIndex(i).stringValue = string.Empty;
            }

            layers.GetArrayElementAtIndex(UnitRenderingLayer.Index).stringValue = UnitRenderingLayer.Name;
            tagManager.ApplyModifiedProperties();
        }

        private static SerializedObject TagManager() =>
            new(AssetDatabase.LoadAllAssetsAtPath(TagManagerPath)[0]);

        private static SerializedProperty Property(SerializedObject settings, string path) =>
            settings.FindProperty(path)
            ?? throw new InvalidOperationException($"HyperRTS: {settings.targetObject.name} has no setting '{path}'.");
    }
}
