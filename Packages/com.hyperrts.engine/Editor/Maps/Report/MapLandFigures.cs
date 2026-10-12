using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Land;

namespace HyperRTS.Editor.Maps.Report
{
    /// <summary>How a map's land is used and what stands on it, as the report stores it: a quick check of balance and budget.</summary>
    [Serializable]
    public sealed class MapLandFigures
    {
        /// <summary>Share of the playable area per land kind, largest first.</summary>
        public List<MapLandShare> shares = new();

        public int fields;
        public int trees;
        public int placements;

        /// <summary>Placements that become entities (gameplay objects and destructible scenery): the simulation's load.</summary>
        public int entities;

        public static MapLandFigures Measure(MapLand land, UnityEngine.Rect playable, int trees,
            IReadOnlyCollection<Placement.MapPlacement> placements)
        {
            var counts = new Dictionary<MapLandKind, int>();
            var fields = new HashSet<int>();
            var total = 0;
            land.ForEachIn(playable, (x, z, _) =>
            {
                var region = land.Region(x, z);
                var kind = region?.Kind ?? MapLandKind.Open;
                counts[kind] = counts.TryGetValue(kind, out var count) ? count + 1 : 1;
                total++;
                if (kind == MapLandKind.Field)
                {
                    fields.Add(land.RegionIndex(x, z));
                }
            });

            return new MapLandFigures
            {
                shares = counts.OrderByDescending(c => c.Value)
                    .Select(c => new MapLandShare { kind = c.Key.ToString(), share = (float)c.Value / total }).ToList(),
                fields = fields.Count,
                trees = trees,
                placements = placements.Count,
                entities = placements.Count(p => p.IsEntity),
            };
        }
    }
}
