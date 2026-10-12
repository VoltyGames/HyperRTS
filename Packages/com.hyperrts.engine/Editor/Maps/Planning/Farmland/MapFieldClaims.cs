using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning.Farmland
{
    /// <summary>
    /// Gives the farmland's free cells to fields: each cell takes the field its sector point falls in, cells near a
    /// field's edge (or the symmetry seam) become its weedy margin, and pieces too small or too narrow to farm become the
    /// kit's leftover ground.
    /// </summary>
    public static class MapFieldClaims
    {
        private const int None = -1;

        public static void Claim(MapPlanContext context, MapFarmlandKit kit, MapFieldGrid grid, List<Vector2> polygon,
            int index, float angle)
        {
            var land = context.Land;
            var fields = Assign(context, grid, polygon);
            var edge = MapLandDistance.ToRegionEdge(land.Width, land.Height, (x, z) => fields[z * land.Width + x]);
            var (sizes, reach) = MapFieldFragments.Measure(fields, edge, land.Width, land.Height);
            var regions = new MapFieldRegions(context, kit, index, angle);
            var noise = new MapNoise(context.Layout.Symmetry, context.Seed, 30);
            var minCells = kit.minField / (MapLand.CellSize * MapLand.CellSize);
            land.ForEach((x, z, p) =>
            {
                var i = z * land.Width + x;
                var field = fields[i];
                if (field == None)
                {
                    return;
                }

                var sector = context.Layout.Symmetry.ToSector(p, out _);
                var width = kit.marginWidth * (1f + 0.45f * noise.Sample(p, 14f));
                var seam = context.Layout.Symmetry.SeamDistance(sector) < kit.marginWidth;
                var atEdge = edge[i] * MapLand.CellSize < width || seam;
                var sliver = sizes[i] < minCells || reach[i] * MapLand.CellSize < kit.minFieldWidth * 0.5f;
                if (atEdge)
                {
                    land.Claim(x, z, regions.Margin);
                    return;
                }

                land.Claim(x, z, sliver ? regions.Leftover(field) : regions.Field(field));
            });
        }

        /// <summary>The field key of every free farmland cell, None elsewhere.</summary>
        private static int[] Assign(MapPlanContext context, MapFieldGrid grid, List<Vector2> polygon)
        {
            var land = context.Land;
            var fields = new int[land.Width * land.Height];
            land.ForEach((x, z, p) =>
            {
                var i = z * land.Width + x;
                fields[i] = None;
                if (!land.IsFree(x, z))
                {
                    return;
                }

                var sector = context.Layout.Symmetry.ToSector(p, out _);
                if (polygon == null || MapGeometry.Contains(polygon, sector))
                {
                    fields[i] = grid.Field(sector);
                }
            });
            return fields;
        }
    }
}
