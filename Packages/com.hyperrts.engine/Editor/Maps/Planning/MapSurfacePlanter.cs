using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Land;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// What grows or lies on each surface by itself: lone trees in meadows and fields, willows on banks, rocks on
    /// ravine rims, bales in rows on stubble. Lands that plant their own trees (woods, belts, ravine floors) are
    /// skipped. Spots are chosen in the authored sector and copied.
    /// </summary>
    public static class MapSurfacePlanter
    {
        public static void Plant(MapPlanContext context)
        {
            var cells = SectorCells(context);
            foreach (var (index, list) in cells)
            {
                var region = context.Land.Regions[index];
                var surface = context.Climate.Surface(region.Surface);
                var hectares = list.Count * MapLand.CellSize * MapLand.CellSize / 10000f;
                var random = Placement.MapPlacer.Random(context.Seed, 31, index);
                if (surface.trees.Count > 0 && !PlantsItself(region.Kind))
                {
                    MapTreeScatter.Scatter(context, surface.trees, hectares, surface.treeSpacing, random,
                        () => RandomSpot(context.Land, list, random), _ => true, $"surface '{surface.key}'");
                }

                foreach (var props in surface.props)
                {
                    MapSurfaceProps.Place(context, region, props, list, hectares, random);
                }
            }
        }

        /// <summary>Cells of the authored sector per region whose surface grows trees or props.</summary>
        private static Dictionary<int, List<int>> SectorCells(MapPlanContext context)
        {
            var land = context.Land;
            var cells = new Dictionary<int, List<int>>();
            var wanted = new bool[land.Regions.Count];
            for (var i = 1; i < land.Regions.Count; i++)
            {
                var surface = context.Climate.Surface(land.Regions[i].Surface);
                wanted[i] = surface != null && (surface.trees.Count > 0 || surface.props.Count > 0);
            }

            land.ForEach((x, z, p) =>
            {
                var region = land.RegionIndex(x, z);
                if (!wanted[region] || !context.Layout.Symmetry.InSector(p))
                {
                    return;
                }

                if (!cells.TryGetValue(region, out var list))
                {
                    cells[region] = list = new List<int>();
                }

                list.Add(z * land.Width + x);
            });
            return cells;
        }

        /// <summary>A random point inside a random cell of the list.</summary>
        public static Vector2 RandomSpot(MapLand land, List<int> cells, System.Random random)
        {
            var cell = cells[random.Next(cells.Count)];
            var jitter = new Vector2((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f);
            return land.Center(cell % land.Width, cell / land.Width) + jitter * MapLand.CellSize;
        }

        private static bool PlantsItself(MapLandKind kind) =>
            kind is MapLandKind.Wood or MapLandKind.WoodEdge or MapLandKind.Belt or MapLandKind.Ravine;
    }
}
