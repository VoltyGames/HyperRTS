using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// The asset a library item is pictured by in lists: the first set ref its <see cref="MapLibraryItemAttribute.PreviewRefs"/>
    /// names (a terrain layer shows its diffuse texture). Items naming none have no picture.
    /// </summary>
    public static class LibraryThumbnail
    {
        public static Object PreviewAsset(Type type, MapLibraryRefs refs)
        {
            foreach (var name in MapLibraryItemAttribute.Require(type).PreviewRefs)
            {
                var path = RefPath(refs, name, type);
                if (!string.IsNullOrEmpty(path))
                {
                    return PreviewAsset(AssetDatabase.LoadMainAssetAtPath(path));
                }
            }

            return null;
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

        private static string RefPath(MapLibraryRefs refs, string name, Type type)
        {
            var field = typeof(MapLibraryRefs).GetField(name);
            var isPath = field?.FieldType == typeof(string);
            var isPathList = field?.FieldType == typeof(List<string>);
            if (!isPath && !isPathList)
            {
                throw new ArgumentException($"HyperRTS: {type.Name} previews by '{name}', which is no asset path ref.");
            }

            if (isPath)
            {
                return (string)field.GetValue(refs);
            }

            // A list of variants previews by its first.
            var paths = (List<string>)field.GetValue(refs);
            return paths.Count > 0 ? paths[0] : null;
        }

        /// <summary>A terrain layer previews as a sphere; its diffuse texture shows the ground better.</summary>
        private static Object PreviewAsset(Object asset) => asset is TerrainLayer layer ? layer.diffuseTexture : asset;
    }
}
