using System.IO;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Maps.Factories;
using HyperRTS.Presentation.Environment.RowCrops;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>
    /// Saves a <see cref="MapCropBake"/> beside the map: the field ids as a lossless picture (R low byte, G high
    /// byte, point-sampled) and a <see cref="RowCropFields"/> asset pointing at it, updated in place so its GUID
    /// stays. A map without row crops gets neither, and stale ones are deleted.
    /// </summary>
    public static class MapCropWriter
    {
        /// <summary>Writes the fields, or deletes them when the map sows no row crops.</summary>
        public static void Write(string assetPath, string picturePath, MapCropBake bake)
        {
            if (bake.IsEmpty)
            {
                AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.DeleteAsset(picturePath);
                return;
            }

            var picture = WritePicture(picturePath, bake);
            var fields = MapAssetFiles.LoadOrCreate(assetPath, ScriptableObject.CreateInstance<RowCropFields>);
            fields.fieldIds = picture;
            fields.area = bake.Area;
            fields.crops = new(bake.Crops);
            fields.fields = new(bake.Fields);
            fields.tiles = new(bake.Tiles);
            fields.tileFields = new(bake.TileFields);
            EditorUtility.SetDirty(fields);
            AssetDatabase.SaveAssetIfDirty(fields);
        }

        private static Texture2D WritePicture(string path, MapCropBake bake)
        {
            var pixels = new Color32[bake.Ids.Length];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = RowCropFields.Encode(bake.Ids[i]);
            }

            var image = new Texture2D(bake.Width, bake.Height, TextureFormat.RGBA32, false, true);
            image.SetPixels32(pixels);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            return EditorTextures.Import(path, importer =>
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 16384;
            });
        }
    }
}
