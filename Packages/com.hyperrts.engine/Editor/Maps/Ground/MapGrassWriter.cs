using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Presentation.Environment.Grass;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>
    /// Saves the painted coverage as a texture array, a slice per plant, and a <see cref="GrassField"/> naming the
    /// plants and the tiles that grow any.
    /// </summary>
    public static class MapGrassWriter
    {
        public static void Write(string fieldPath, string coveragePath, MapGrassBake bake, List<MapClimateDetail> plants)
        {
            if (bake.IsEmpty)
            {
                AssetDatabase.DeleteAsset(fieldPath);
                AssetDatabase.DeleteAsset(coveragePath);
                return;
            }

            var coverage = WriteCoverage(coveragePath, bake);
            var field = AssetDatabase.LoadAssetAtPath<GrassField>(fieldPath);
            if (field == null)
            {
                field = ScriptableObject.CreateInstance<GrassField>();
                AssetDatabase.CreateAsset(field, fieldPath);
            }

            var (tiles, tilePlants) = bake.Tiles();
            field.coverage = coverage;
            field.area = bake.Area;
            field.plants = plants.ConvertAll(plant => plant.Plant());
            field.tiles = tiles;
            field.tilePlants = tilePlants;
            EditorUtility.SetDirty(field);
            AssetDatabase.SaveAssetIfDirty(field);
        }

        /// <summary>Linear, filtered and clamped: the kernel reads coverage between texels.</summary>
        public static Texture2DArray Coverage(MapGrassBake bake)
        {
            var size = bake.Resolution;
            var array = new Texture2DArray(size, size, bake.Layers.Length, TextureFormat.R8, false, true)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Grass Coverage",
            };
            for (var p = 0; p < bake.Layers.Length; p++)
            {
                array.SetPixelData(bake.Layers[p], 0, p);
            }

            array.Apply(false);
            return array;
        }

        // Recreated, not updated in place: only the field, which the scene names, needs a stable GUID.
        private static Texture2DArray WriteCoverage(string path, MapGrassBake bake)
        {
            var coverage = Coverage(bake);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(coverage, path);
            return coverage;
        }
    }
}
