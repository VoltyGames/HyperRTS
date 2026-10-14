using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Editor.Validation
{
    /// <summary>The active URP renderers and their renderer features, for validation checks and quick fixes.</summary>
    public static class RendererFeatures
    {
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

        public static T Find<T>(UniversalRendererData data) where T : ScriptableRendererFeature =>
            data.rendererFeatures.OfType<T>().FirstOrDefault();

        /// <summary>Adds a new feature the way the renderer inspector does.</summary>
        public static void Add(UniversalRendererData data, ScriptableRendererFeature feature, string name)
        {
            feature.name = name;
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

        private static SerializedProperty Property(SerializedObject settings, string path) =>
            settings.FindProperty(path)
            ?? throw new InvalidOperationException($"HyperRTS: {settings.targetObject.name} has no setting '{path}'.");
    }
}
