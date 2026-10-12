using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Texturing;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// Builds a climate from its library file: the surfaces, road classes and kits it lists, then the terrain layers,
    /// trees and plants those use, each added the first time it is named. The walk goes surfaces, belt kits, woodland
    /// kits, the reeds, then settlement orchards, so the order (and every layer, tree and plant index) only changes
    /// when the data does.
    /// </summary>
    internal sealed class MapClimateAssembly
    {
        private readonly MapLibrary _library;
        private readonly MapClimate _climate;

        private MapClimateAssembly(MapLibrary library, MapClimate climate)
        {
            _library = library;
            _climate = climate;
        }

        public static MapClimate Assemble(MapLibrary library, string key, string referrer)
        {
            var climate = library.Get<MapClimate>(key, referrer, out var refs);
            climate.surfaces = All<MapClimateSurface>(library, refs.surfaces, refs.file);
            climate.roadClasses = All<MapRoadClass>(library, refs.roadClasses, refs.file);
            climate.farmlandKits = All<MapFarmlandKit>(library, refs.farmlandKits, refs.file);
            climate.beltKits = All<MapBeltKit>(library, refs.beltKits, refs.file);
            climate.woodlandKits = All<MapWoodlandKit>(library, refs.woodlandKits, refs.file);
            climate.layers = new List<MapClimateLayer>();
            climate.trees = new List<MapClimateTree>();
            climate.details = new List<MapClimateDetail>();
            new MapClimateAssembly(library, climate).Collect(refs.file);
            if (climate.layers.Count > MapTerrainTextures.MaxLayers)
            {
                throw new InvalidOperationException(
                    $"HyperRTS: climate '{refs.file}' uses {climate.layers.Count} terrain layers; the terrain shader blends at most {MapTerrainTextures.MaxLayers}.");
            }

            return climate;
        }

        private static List<T> All<T>(MapLibrary library, List<string> keys, string referrer)
            where T : class, IMapLibraryItem, new() =>
            keys.ConvertAll(key => library.Get<T>(key, referrer));

        private void Collect(string climateFile)
        {
            _climate.surfaces.ForEach(Surface);
            _climate.beltKits.ForEach(BeltKit);
            _climate.woodlandKits.ForEach(WoodlandKit);
            Detail(_climate.reedDetail, climateFile);
            foreach (var key in _library.Keys<MapSettlementKit>())
            {
                var kit = _library.Get<MapSettlementKit>(key, null);
                Tree(kit.orchardTree, File<MapSettlementKit>(key));
            }
        }

        private void Surface(MapClimateSurface surface)
        {
            var file = File<MapClimateSurface>(surface.key);
            Add(_climate.layers, surface.layer, file);
            surface.trees.ForEach(tree => Tree(tree.tree, file));
            surface.details.ForEach(detail => Detail(detail.detail, file));
        }

        private void BeltKit(MapBeltKit kit)
        {
            var file = File<MapBeltKit>(kit.key);
            kit.trees.ForEach(tree => Tree(tree.tree, file));
            Tree(kit.shrub, file);
        }

        private void WoodlandKit(MapWoodlandKit kit)
        {
            var file = File<MapWoodlandKit>(kit.key);
            kit.trees.ForEach(tree => Tree(tree.tree, file));
            kit.edgeTrees.ForEach(tree => Tree(tree.tree, file));
        }

        private void Tree(string key, string referrer) => Add(_climate.trees, key, referrer);

        private void Detail(string key, string referrer) => Add(_climate.details, key, referrer);

        private void Add<T>(List<T> items, string key, string referrer) where T : class, IMapLibraryItem, IMapKeyed, new()
        {
            if (string.IsNullOrEmpty(key) || MapKeys.Contains(items, key))
            {
                return;
            }

            items.Add(_library.Get<T>(key, referrer));
        }

        private string File<T>(string key) => _library.FilePath<T>(key);
    }
}
