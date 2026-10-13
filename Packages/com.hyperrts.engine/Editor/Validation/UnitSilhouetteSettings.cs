using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Presentation.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
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

        /// <summary>The URP renderers of the active quality level's pipeline asset.</summary>
        public static List<UniversalRendererData> ActiveRenderers()
        {
            var renderers = new List<UniversalRendererData>();
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset asset)
            {
                return renderers;
            }

            foreach (var data in asset.rendererDataList)
            {
                if (data is UniversalRendererData universal)
                {
                    renderers.Add(universal);
                }
            }

            return renderers;
        }

        public static bool HasFeature(UniversalRendererData data) =>
            data.rendererFeatures.OfType<UnitSilhouetteFeature>().Any();

        /// <summary>Adds the feature the way the renderer inspector does.</summary>
        public static void AddFeature(UniversalRendererData data)
        {
            var feature = ScriptableObject.CreateInstance<UnitSilhouetteFeature>();
            feature.name = FeatureName;
            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.SaveAssets();
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long id);
            var serialized = new SerializedObject(data);
            var features = Property(serialized, "m_RendererFeatures");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            var map = Property(serialized, "m_RendererFeatureMap");
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

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
