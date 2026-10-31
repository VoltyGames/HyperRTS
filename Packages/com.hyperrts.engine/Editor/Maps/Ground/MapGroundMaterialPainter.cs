using System.Collections.Generic;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Simulation.Cues;
using UnityEditor;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>
    /// Paints one <see cref="SurfaceMaterial"/> per land cell: the region's surface, a road region's class, or
    /// water.
    /// </summary>
    public sealed class MapGroundMaterialPainter
    {
        private readonly MapLand _land;
        private readonly MapClimate _climate;
        private readonly Dictionary<string, SurfaceMaterial> _surfaces = new();
        private readonly Dictionary<MapLandRegion, SurfaceMaterial> _roads = new();

        private MapGroundMaterialPainter(MapLand land, MapClimate climate)
        {
            (_land, _climate) = (land, climate);
        }

        /// <summary>Saves the painted grid to <paramref name="path"/>, keeping the asset's GUID across rebuilds.</summary>
        public static GroundMaterialMap Write(string path, MapLand land, MapClimate climate)
        {
            var map = EditorAssets.LoadOrCreate<GroundMaterialMap>(path);

            (map.width, map.height, map.spacing) = (land.Width, land.Height, MapLand.CellSize);
            map.cells = Paint(land, climate);
            EditorUtility.SetDirty(map);
            AssetDatabase.SaveAssetIfDirty(map);
            return map;
        }

        /// <summary>
        /// Row-major cells from the land's south-west corner, each a <see cref="SurfaceMaterial"/> byte.
        /// </summary>
        public static byte[] Paint(MapLand land, MapClimate climate)
        {
            var painter = new MapGroundMaterialPainter(land, climate);
            var cells = new byte[land.Width * land.Height];
            land.ForEach((x, z, _) => cells[z * land.Width + x] = (byte)painter.Cell(x, z));
            return cells;
        }

        private SurfaceMaterial Cell(int x, int z)
        {
            var region = _land.Region(x, z);
            if (region == null)
            {
                return Surface(_climate.groundSurface);
            }

            return region.Kind switch
            {
                MapLandKind.Water => SurfaceMaterial.Water,
                MapLandKind.Road => Road(region),
                _ => Surface(region.Surface),
            };
        }

        private SurfaceMaterial Road(MapLandRegion region)
        {
            if (!_roads.TryGetValue(region, out var material))
            {
                material = MapImpactClass.Parse($"road region painting '{region.Surface}'", region.ImpactClass);
                _roads.Add(region, material);
            }

            return material;
        }

        private SurfaceMaterial Surface(string key)
        {
            if (!_surfaces.TryGetValue(key, out var material))
            {
                material = MapImpactClass.Parse($"surface '{key}'", _climate.Surface(key).impactClass);
                _surfaces.Add(key, material);
            }

            return material;
        }
    }
}
