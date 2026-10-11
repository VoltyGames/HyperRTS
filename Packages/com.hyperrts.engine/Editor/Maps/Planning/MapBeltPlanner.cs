using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// Shelterbelts and hedgerows: a strip of ground along a polyline with rows of trees, shrubs in the gaps and on
    /// the edges, and breaks where tracks cut through. Used for spec belts and the farmland planner's field-end belts.
    /// </summary>
    public static class MapBeltPlanner
    {
        private const float Jitter = 0.25f;
        private const float MinSpacing = 2f;

        public static void PlanSpecBelts(MapPlanContext context)
        {
            foreach (var belt in context.Layout.Belts)
            {
                var kit = Kit(context, belt.Placed.kind, $"belt {belt.Index + 1}");
                if (kit == null)
                {
                    continue;
                }

                var line = MapGeometry.ToVectors(belt.Placed.points);
                var gaps = Gaps(kit, MapGeometry.Length(line), context.Seed, belt.Index);
                Claim(context, kit, line, gaps, belt.Index);
                if (belt.Copy == 0)
                {
                    Plant(context, kit, line, gaps, belt.Index);
                }
            }
        }

        /// <summary>A belt laid by a planner along an authored-sector polyline: claimed and planted in every copy.</summary>
        public static void PlanLine(MapPlanContext context, MapBeltKit kit, List<Vector2> sectorLine, int seed)
        {
            var gaps = Gaps(kit, MapGeometry.Length(sectorLine), context.Seed, seed);
            foreach (var copy in context.Layout.Symmetry.Copies)
            {
                Claim(context, kit, sectorLine.ConvertAll(copy.Apply), gaps, seed);
            }

            Plant(context, kit, sectorLine, gaps, seed);
        }

        public static MapBeltKit Kit(MapPlanContext context, string key, string source)
        {
            var kit = context.Climate.BeltKit(key);
            if (kit == null)
            {
                context.Issues.Error($"{source}: belt kit '{key}' is not in climate '{context.Climate.name}'.");
            }

            return kit;
        }

        private static void Claim(MapPlanContext context, MapBeltKit kit, List<Vector2> line, List<Vector2> gaps, int seed)
        {
            var region = context.Region(MapLandKind.Belt, kit.surface, $"belt kit '{kit.key}'", seed);
            var half = kit.width * 0.5f;
            context.Land.ClaimWhere(MapGeometry.Bounds(line, half + 1f), region, p =>
            {
                var distance = MapGeometry.DistanceToPolyline(line, p, out var along);
                return distance < half && !InGap(gaps, along);
            });
        }

        private static void Plant(MapPlanContext context, MapBeltKit kit, List<Vector2> line, List<Vector2> gaps, int seed)
        {
            var random = Placement.MapPlacer.Random(context.Seed, 28, seed);
            var length = MapGeometry.Length(line);
            var rowGap = kit.rows > 1 ? (kit.width - 2f) / kit.rows : 0f;
            for (var row = 0; row < kit.rows; row++)
            {
                var offset = (row - (kit.rows - 1) * 0.5f) * rowGap;
                var start = random.NextDouble() * kit.spacing;
                for (var along = (float)start; along < length; along += kit.spacing * (1f + Jitter * ((float)random.NextDouble() - 0.5f)))
                {
                    if (!InGap(gaps, along))
                    {
                        PlantAt(context, kit, line, along, offset, random);
                    }
                }
            }
        }

        private static void PlantAt(MapPlanContext context, MapBeltKit kit, List<Vector2> line, float along,
            float offset, System.Random random)
        {
            var point = MapGeometry.PointAlong(line, along, out var direction);
            var side = new Vector2(direction.y, -direction.x);
            var shrub = !string.IsNullOrEmpty(kit.shrub) && random.NextDouble() < kit.shrubShare;
            var key = shrub ? kit.shrub : MapPlanContext.Pick(kit.trees, t => t.weight, random).tree;
            var jitter = ((float)random.NextDouble() - 0.5f) * 1.2f;
            var spot = point + side * (offset + jitter);
            if (!context.Layout.Symmetry.InSector(spot) || !context.Spacing.Fits(spot, MinSpacing))
            {
                return;
            }

            var tree = context.Tree(key, $"belt kit '{kit.key}'");
            if (tree >= 0)
            {
                context.PlantCopies(tree, spot, context.TreeScale(tree, random), (float)random.NextDouble() * 360f);
            }
        }

        /// <summary>Gap centres along the belt (x) and their half widths (y).</summary>
        private static List<Vector2> Gaps(MapBeltKit kit, float length, int seed, int index)
        {
            var gaps = new List<Vector2>();
            if (kit.gapEvery <= 0f)
            {
                return gaps;
            }

            var random = Placement.MapPlacer.Random(seed, 29, index);
            for (var at = kit.gapEvery * (0.4f + 0.4f * (float)random.NextDouble()); at < length; at += kit.gapEvery * (0.7f + 0.6f * (float)random.NextDouble()))
            {
                gaps.Add(new Vector2(at, kit.gapWidth * 0.5f));
            }

            return gaps;
        }

        private static bool InGap(List<Vector2> gaps, float along)
        {
            foreach (var gap in gaps)
            {
                if (Mathf.Abs(along - gap.x) < gap.y)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
