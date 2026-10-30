using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Generation;
using HyperRTS.Editor.Maps.Land;

namespace HyperRTS.Editor.Maps.Report
{
    /// <summary>How a map's land is used and what stands on it, as the report stores it.</summary>
    [Serializable]
    public sealed class MapLandFigures
    {
        /// <summary>Share of the playable area per land kind, largest first.</summary>
        public List<MapLandShare> shares = new();

        public int fields;
        public int trees;
        public int placements;

        /// <summary>Ground decals; the rules' maxDecals caps them and the wet ones together.</summary>
        public int decals;

        /// <summary>Surface decals shown only after rain.</summary>
        public int wetDecals;

        /// <summary>Placements that become entities, the simulation's load.</summary>
        public int entities;

        /// <summary>16 m tiles holding row crops, which the crop renderer culls one by one.</summary>
        public int cropTiles;

        /// <summary>Row crop plants at full density, estimated from sown area and spacing.</summary>
        public long cropPlants;

        /// <summary>Props per hectare of each surface.</summary>
        public List<MapSurfaceProps> props = new();

        public static MapLandFigures Measure(MapGeneration generation)
        {
            var (land, placements) = (generation.Land, generation.Placements.All);
            var counts = new Dictionary<MapLandKind, int>();
            var fields = new HashSet<int>();
            var total = 0;
            land.ForEachIn(generation.Layout.Bounds, (x, z, _) =>
            {
                var region = land.Region(x, z);
                var kind = region?.Kind ?? MapLandKind.Open;
                counts[kind] = counts.TryGetValue(kind, out var count) ? count + 1 : 1;
                total++;
                // Headlands share their field's seed.
                if (kind == MapLandKind.Field)
                {
                    fields.Add(region.Seed);
                }
            });

            return new MapLandFigures
            {
                shares = counts.OrderByDescending(c => c.Value)
                    .Select(c => new MapLandShare { kind = c.Key.ToString(), share = (float)c.Value / total }).ToList(),
                fields = fields.Count,
                trees = generation.Trees.Count,
                placements = placements.Count,
                decals = generation.Decals.Count,
                wetDecals = generation.WetDecals.Count,
                entities = placements.Count(p => p.IsEntity),
                cropTiles = generation.Crops.Tiles.Count,
                cropPlants = generation.Crops.Plants,
                props = MapSurfaceProps.Measure(land, placements, generation.Layout.Bounds),
            };
        }
    }
}
