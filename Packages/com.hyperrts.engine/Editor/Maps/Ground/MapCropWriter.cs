using HyperRTS.Editor.Common;
using HyperRTS.Presentation.Environment.RowCrops;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>Saves field ids as a lossless picture (R low byte, G high byte) and a <see cref="RowCropFields"/> asset updated in place to keep its GUID.</summary>
    public static class MapCropWriter
    {
        public static void Write(string assetPath, string picturePath, MapCropBake bake)
        {
            if (bake.IsEmpty)
            {
                AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.DeleteAsset(picturePath);
                return;
            }

            var picture = WritePicture(picturePath, bake);
            var fields = EditorAssets.LoadOrCreate<RowCropFields>(assetPath);

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

            return EditorTextures.WritePng(path, bake.Width, bake.Height, TextureFormat.RGBA32, pixels, importer =>
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
