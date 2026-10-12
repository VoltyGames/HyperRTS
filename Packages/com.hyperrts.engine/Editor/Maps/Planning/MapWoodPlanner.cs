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
    public sealed class MapWoodPlanner : MapLandPass
    {
        private const float EdgeWobble = 2.5f;
        private const float ClearingWavelength = 35f;
        private MapNoise _clearings;

        public MapWoodPlanner(MapPlanContext context) : base(context)
        {
        }

        public override void Plan()
        {
            _clearings = Context.Noise(MapNoiseChannel.WoodClearings);
            var edges = Context.Noise(MapNoiseChannel.WoodEdges);
            foreach (var wood in Context.Layout.Woods)
            {
                var kit = Climate.WoodlandKit(wood.Placed.kind);
                Claim(wood, kit, edges);
                if (wood.Copy == 0)
                {
                    Plant(wood, kit);
                }
            }
        }

        private void Claim(MapCopy<MapWoodSpec> wood, MapWoodlandKit kit, MapNoise edges)
        {
            var polygon = MapGeometry.ToVectors(wood.Placed.points);
            var floor = Context.Region(MapLandKind.Wood, kit.surface, wood.Index);
            var edge = Context.Region(MapLandKind.WoodEdge, kit.edgeSurface, wood.Index);
            var threshold = Threshold(kit);
            Land.ForEachIn(MapGeometry.Bounds(polygon), (x, z, p) =>
            {
                var inside = MapGeometry.SignedDistance(polygon, p) + edges.Sample(p, 10f) * EdgeWobble;
                if (inside <= 0f || !Land.IsFree(x, z))
                {
                    return;
                }

                var open = inside < kit.edgeWidth || _clearings.Sample(p, ClearingWavelength) > threshold;
                Land.Claim(x, z, open ? edge : floor);
            });
        }

        private void Plant(MapCopy<MapWoodSpec> wood, MapWoodlandKit kit)
        {
            var polygon = MapGeometry.ToVectors(wood.Authored.points);
            var bounds = MapGeometry.Bounds(polygon);
            var area = MapGeometry.Area(polygon) / 10000f;
            var edgeArea = Perimeter(polygon) * kit.edgeWidth / 10000f;
            var threshold = Threshold(kit);
            var random = Context.Random(MapRandomChannel.WoodTrees, wood.Index);
            Context.Trees.Scatter(kit.trees, Mathf.Max(0f, area - edgeArea) * (1f - kit.clearings), kit.spacing, random,
                () => MapGeometry.RandomIn(bounds, random), p =>
                {
                    var depth = MapGeometry.SignedDistance(polygon, p);
                    var canopy = depth > kit.edgeWidth * 0.5f && _clearings.Sample(p, ClearingWavelength) <= threshold;
                    return canopy && Symmetry.InSector(p);
                });
            Context.Trees.Scatter(kit.edgeTrees, edgeArea, kit.spacing * 0.6f, random,
                () => MapGeometry.RandomIn(bounds, random), p =>
                {
                    var depth = MapGeometry.SignedDistance(polygon, p);
                    return depth > 0f && depth < kit.edgeWidth && Symmetry.InSector(p);
                });
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
