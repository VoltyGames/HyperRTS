using HyperRTS.Editor.Maps.Data;

namespace HyperRTS.Editor.Tests
{
    /// <summary>Climate surfaces with every required field set, for tests that build climates inline.</summary>
    public static class MapClimateFixture
    {
        public static MapClimateSurface Surface(string key, string layer, float edgeWidth = 1f, float edgeJitter = 1f) =>
            new() { key = key, layer = layer, impactClass = "Grass", edgeWidth = edgeWidth, edgeJitter = edgeJitter };

        /// <summary>One terrain layer and one surface painting it per key.</summary>
        public static void AddSurfaces(MapClimate climate, params string[] keys) => AddSurfaces(climate, 1f, 1f, keys);

        public static void AddSurfaces(MapClimate climate, float edgeWidth, float edgeJitter, params string[] keys)
        {
            foreach (var key in keys)
            {
                climate.layers.Add(new MapClimateLayer { key = key });
                climate.surfaces.Add(Surface(key, key, edgeWidth, edgeJitter));
            }
        }
    }
}
