using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Vision.Sight;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Vegetation
{
    /// <summary>Paints each crop cell's sight cover: its crop's sight density and height, from the crop bake.</summary>
    public static class MapCropCoverPainter
    {
        /// <summary>Saves the cover to <paramref name="path"/>, keeping the asset's GUID across rebuilds.</summary>
        public static CropCoverMap Write(string path, MapCropBake bake)
        {
            var map = EditorAssets.LoadOrCreate(path, ScriptableObject.CreateInstance<CropCoverMap>);
            (map.width, map.height, map.spacing) = (bake.Width, bake.Height, bake.Area.width / bake.Width);
            map.density = new byte[bake.Ids.Values.Length];
            map.top = new byte[map.density.Length];
            for (var i = 0; i < map.density.Length; i++)
            {
                var id = bake.Ids[i];
                if (id == 0)
                {
                    continue;
                }

                var crop = bake.Fields[id - 1].crop;
                map.density[i] = Steps(bake.SightDensities[crop], SightBlockers.DensityStep);
                map.top[i] = Steps(bake.Crops[crop].height, SightBlockers.Step);
            }

            EditorUtility.SetDirty(map);
            AssetDatabase.SaveAssetIfDirty(map);
            return map;
        }

        private static byte Steps(float value, float step) => (byte)Mathf.Clamp(Mathf.RoundToInt(value / step), 0, 255);
    }
}
