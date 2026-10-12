using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Factories
{
    /// <summary>Loads a generated asset to update in place, creating it on the first run so its GUID never changes.</summary>
    public static class MapAssetFiles
    {
        public static T LoadOrCreate<T>(string path, System.Func<T> create) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = create();
                AssetDatabase.CreateAsset(asset, path);
            }

            return asset;
        }

        /// <summary>A material at <paramref name="path"/> on <paramref name="shader"/> (set again on an existing one), instanced, marked dirty.</summary>
        public static Material Material(string path, string shader)
        {
            var found = Shader.Find(shader) ?? throw new System.InvalidOperationException($"HyperRTS: shader '{shader}' is missing.");
            var material = LoadOrCreate(path, () => new Material(found));
            material.shader = found;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
