using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Build
{
    /// <summary>
    /// Bakes the open map's environment lighting only (sky ambient and the default reflection), with no lightmaps:
    /// units and the day light are all real-time, but ambient and reflections need the sky baked once.
    /// </summary>
    public static class MapLightingBaker
    {
        public static void Bake(MapPaths paths)
        {
            var path = $"{paths.Folder}/{paths.Name}_Lighting.lighting";
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
            if (settings == null)
            {
                settings = new LightingSettings { name = paths.Name + " Lighting" };
                AssetDatabase.CreateAsset(settings, path);
            }

            settings.bakedGI = false;
            settings.realtimeGI = false;
            settings.autoGenerate = false;
            EditorUtility.SetDirty(settings);
            Lightmapping.lightingSettings = settings;
            if (!Lightmapping.Bake())
            {
                Debug.LogError($"HyperRTS: the environment lighting bake for '{paths.Name}' failed.");
            }
        }
    }
}
