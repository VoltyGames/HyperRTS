using System.IO;
using HyperRTS.Editor.Common;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Scenes
{
    /// <summary>Keeps a captured overview next to the map as a texture asset, for loading screens and menus.</summary>
    public static class MapOverviewPicture
    {
        private const int MaxSize = 2048;

        public static Texture2D Keep(string captured, string assetPath)
        {
            if (captured == null || !File.Exists(captured))
            {
                throw new FileNotFoundException("HyperRTS: the build captured no overview view to keep.", captured);
            }

            File.Copy(captured, assetPath, true);
            return EditorTextures.Import(assetPath, importer =>
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = MaxSize;
            });
        }
    }
}
