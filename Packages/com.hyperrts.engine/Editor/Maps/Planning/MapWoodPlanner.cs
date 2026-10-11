using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// Spec woods: forest floor under the canopy, a shrubby band round the edge and in glades, canopy trees inside
    /// and shrubs with young trees at the edge, so a wood fades into the open instead of stopping at a wall.
    /// </summary>
    public static class MapWoodPlanner
    {
        private const float EdgeWobble = 2.5f;
        private const float ClearingWavelength = 35f;

        public static void Plan(MapPlanContext context)
        {
            var clearings = new MapNoise(context.Layout.Symmetry, context.Seed, 25);
            var wobble = new MapNoise(context.Layout.Symmetry, context.Seed, 26);
            foreach (var wood in context.Layout.Woods)
            {
                var kit = context.Climate.WoodlandKit(wood.Placed.kind);
                if (kit == null)
                {
                    context.Issues.Error($"Wood {wood.Index + 1}: woodland kit '{wood.Placed.kind}' is not in climate '{context.Climate.name}'.");
                    continue;
                }

                Claim(context, wood, kit, clearings, wobble);
                if (wood.Copy == 0)
                {
                    Plant(context, wood, kit, clearings);
                }
            }
        }

        private static void Claim(MapPlanContext context, MapCopy<MapWoodSpec> wood, MapWoodlandKit kit,
            MapNoise clearings, MapNoise wobble)
        {
            var polygon = MapGeometry.ToVectors(wood.Placed.points);
            var source = $"wood {wood.Index + 1}";
            var floor = context.Region(MapLandKind.Wood, kit.surface, source, wood.Index);
            var edge = context.Region(MapLandKind.WoodEdge, kit.edgeSurface, source, wood.Index);
            var threshold = Threshold(kit);
            context.Land.ForEachIn(MapGeometry.Bounds(polygon), (x, z, p) =>
            {
                var inside = MapGeometry.SignedDistance(polygon, p) + wobble.Sample(p, 10f) * EdgeWobble;
                if (inside <= 0f || !context.Land.IsFree(x, z))
                {
                    return;
                }

                var open = inside < kit.edgeWidth || clearings.Sample(p, ClearingWavelength) > threshold;
                context.Land.Claim(x, z, open ? edge : floor);
            });
        }

        private static void Plant(MapPlanContext context, MapCopy<MapWoodSpec> wood, MapWoodlandKit kit,
            MapNoise clearings)
        {
            var polygon = MapGeometry.ToVectors(wood.Authored.points);
            var bounds = MapGeometry.Bounds(polygon);
            var area = MapGeometry.Area(polygon) / 10000f;
            var edgeArea = Perimeter(polygon) * kit.edgeWidth / 10000f;
            var threshold = Threshold(kit);
            var random = Placement.MapPlacer.Random(context.Seed, 27, wood.Index);
            var source = $"wood {wood.Index + 1}";
            MapTreeScatter.Scatter(context, kit.trees, Mathf.Max(0f, area - edgeArea) * (1f - kit.clearings),
                kit.spacing, random, () => MapTreeScatter.In(bounds, random), p =>
                {
                    var depth = MapGeometry.SignedDistance(polygon, p);
                    var canopy = depth > kit.edgeWidth * 0.5f && clearings.Sample(p, ClearingWavelength) <= threshold;
                    return canopy && context.Layout.Symmetry.InSector(p);
                }, source);
            MapTreeScatter.Scatter(context, kit.edgeTrees, edgeArea, kit.spacing * 0.6f, random,
                () => MapTreeScatter.In(bounds, random), p =>
                {
                    var depth = MapGeometry.SignedDistance(polygon, p);
                    return depth > 0f && depth < kit.edgeWidth && context.Layout.Symmetry.InSector(p);
                }, source);
        }

        /// <summary>Noise above this is a glade, so about the kit's share of the wood stays open.</summary>
        private static float Threshold(MapWoodlandKit kit) => 1f - 2f * kit.clearings;

        private static float Perimeter(IReadOnlyList<Vector2> polygon)
        {
            var length = 0f;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                length += (polygon[i] - polygon[j]).magnitude;
            }

            return length;
        }
    }
}
