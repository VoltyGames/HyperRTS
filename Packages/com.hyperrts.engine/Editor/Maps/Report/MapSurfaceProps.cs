using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Placement;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Report
{
    /// <summary>How densely one surface of the playable area is dressed with props.</summary>
    [Serializable]
    public sealed class MapSurfaceProps
    {
        private const float SquareMetresPerHectare = 10000f;

        public string surface;
        public float hectares;
        public float perHectare;

        /// <summary>Every claimed surface in <paramref name="area"/>, most dressed first.</summary>
        public static List<MapSurfaceProps> Measure(MapLand land, IEnumerable<MapPlacement> placements, Rect area)
        {
            var cells = new Dictionary<string, int>();
            land.ForEachIn(area, (x, z, _) =>
            {
                var surface = land.Region(x, z)?.Surface;
                if (surface != null)
                {
                    cells[surface] = cells.TryGetValue(surface, out var count) ? count + 1 : 1;
                }
            });

            var props = new Dictionary<string, int>();
            foreach (var placement in placements.Where(p => area.Contains(p.Position)))
            {
                var surface = land.At(placement.Position)?.Surface;
                if (surface != null)
                {
                    props[surface] = props.TryGetValue(surface, out var count) ? count + 1 : 1;
                }
            }

            return cells.Select(c => Density(c.Key, c.Value, props.TryGetValue(c.Key, out var n) ? n : 0))
                .OrderByDescending(d => d.perHectare).ToList();
        }

        private static MapSurfaceProps Density(string surface, int cells, int props)
        {
            var hectares = cells * MapLand.CellSize * MapLand.CellSize / SquareMetresPerHectare;
            return new MapSurfaceProps { surface = surface, hectares = hectares, perHectare = props / hectares };
        }
    }
}
