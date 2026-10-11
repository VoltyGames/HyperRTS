using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// Gives the farmland's free cells to fields: each cell takes the field its sector point falls in, cells near a
    /// field's edge (or the symmetry seam) become its weedy margin, and slivers too small to farm join the margin.
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
            var sizes = MapFieldFragments.Sizes(fields, land.Width, land.Height);
            var margin = context.Region(MapLandKind.Margin, kit.marginSurface, $"farmland {index + 1} margin", index);
            var regions = new Dictionary<int, int>();
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
                if (edge[i] * MapLand.CellSize < width || seam || sizes[i] < minCells)
                {
                    land.Claim(x, z, margin);
                    return;
                }

                if (!regions.TryGetValue(field, out var region))
                {
                    regions[field] = region = FieldRegion(context, kit, field, index, angle);
                }

                land.Claim(x, z, region);
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

        /// <summary>A field's crop is picked from its key, so every copy of the field grows the same thing.</summary>
        private static int FieldRegion(MapPlanContext context, MapFarmlandKit kit, int field, int index, float angle)
        {
            var seed = unchecked(field * 73856093 ^ index * 19349663 ^ context.Seed);
            var random = new System.Random(seed);
            var crop = MapPlanContext.Pick(kit.crops, c => c.weight, random);
            return context.Region(MapLandKind.Field, crop.surface, $"farmland kit '{kit.key}'", seed, angle);
        }
    }
}
