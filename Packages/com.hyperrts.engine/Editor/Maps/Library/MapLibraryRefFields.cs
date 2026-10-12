using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// The <see cref="MapLibraryRefs"/> fields each library item class reads (asset paths and key lists), so editors
    /// show and write only those, in this order, ahead of the item's own fields.
    /// </summary>
    public static class MapLibraryRefFields
    {
        private static readonly Dictionary<Type, string[]> Fields = new()
        {
            { typeof(MapClimateLayer), new[] { "terrainLayer" } },
            { typeof(MapClimateDetail), new[] { "mesh", "texture" } },
            { typeof(MapClimateTree), new[] { "prefab", "destructible" } },
            { typeof(MapRoadClass), new[] { "material" } },
            { typeof(MapClimateSurface), Array.Empty<string>() },
            { typeof(MapFarmlandKit), Array.Empty<string>() },
            { typeof(MapBeltKit), Array.Empty<string>() },
            { typeof(MapWoodlandKit), Array.Empty<string>() },
            { typeof(MapSettlementKit), Array.Empty<string>() },
            { typeof(MapPaletteEntry), new[] { "prefab" } },
            {
                typeof(MapClimate),
                new[] { "surfaces", "roadClasses", "farmlandKits", "beltKits", "woodlandKits", "water", "atmosphere", "skybox" }
            },
            { typeof(MapPalette), new[] { "rig", "startingResources" } },
            { typeof(MapRules), Array.Empty<string>() },
        };

        public static IReadOnlyList<string> Of(Type type) => Fields.TryGetValue(type, out var fields)
            ? fields
            : throw new ArgumentException($"HyperRTS: {type.Name} is not a map library item class.", nameof(type));
    }
}
