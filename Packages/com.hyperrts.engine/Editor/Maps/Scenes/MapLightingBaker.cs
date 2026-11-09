using HyperRTS.Editor.Common;
using HyperRTS.Editor.Maps.Build;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Scenes
{
    /// <summary>
    /// Bakes the open map's environment lighting only (sky ambient and the default reflection), with no lightmaps:
    /// units and the day light are all real-time, but ambient and reflections need the sky baked once.
    /// </summary>
    public static class MapLightingBaker
    {
        public static void Bake(MapPaths paths)
        {
            var path = paths.Lighting;
            var settings = EditorAssets.LoadOrCreate(path, () => new LightingSettings { name = paths.Name + " Lighting" });
            settings.bakedGI = false;
            EditorUtility.SetDirty(settings);
            Lightmapping.lightingSettings = settings;
            if (!Lightmapping.Bake())
            {
                Debug.LogError($"HyperRTS: the environment lighting bake for '{paths.Name}' failed.");
            }
        }
    }
}
