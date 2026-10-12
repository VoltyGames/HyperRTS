using System;
using HyperRTS.Editor.Maps.Data;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// The asset a library item is pictured by in lists: a layer's diffuse texture, a plant's texture or mesh, a
    /// tree's or prop's prefab, a road's material. Kits, climates, palettes and rules have none.
    /// </summary>
    public static class LibraryThumbnail
    {
        public static Object AssetOf(Type type, MapLibraryRefs refs)
        {
            if (type == typeof(MapClimateLayer))
            {
                var layer = Load<TerrainLayer>(refs.terrainLayer);
                return layer == null ? null : layer.diffuseTexture;
            }

            if (type == typeof(MapClimateDetail))
            {
                return string.IsNullOrEmpty(refs.texture) ? Load<GameObject>(refs.mesh) : Load<Texture2D>(refs.texture);
            }

            if (type == typeof(MapClimateTree) || type == typeof(MapPaletteEntry))
            {
                return Load<GameObject>(refs.prefab);
            }

            return type == typeof(MapRoadClass) ? Load<Material>(refs.material) : null;
        }

        /// <summary>The rendered preview of <paramref name="asset"/>, or its icon while the preview is still loading.</summary>
        public static Texture Image(Object asset)
        {
            if (asset == null)
            {
                return null;
            }

            var preview = AssetPreview.GetAssetPreview(asset);
            return preview != null ? preview : AssetPreview.GetMiniThumbnail(asset);
        }

        private static T Load<T>(string path) where T : Object =>
            string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
    }
}
