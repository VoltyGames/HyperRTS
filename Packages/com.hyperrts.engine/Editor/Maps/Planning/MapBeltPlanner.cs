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
                var kit = context.Climate.BeltKit(belt.Placed.kind);
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

        private static void Claim(MapPlanContext context, MapBeltKit kit, List<Vector2> line, List<Vector2> gaps, int seed)
        {
            var region = context.Region(MapLandKind.Belt, kit.surface, seed);
            var half = kit.width * 0.5f;
            context.Land.ClaimWhere(MapGeometry.Bounds(line, half + 1f), region, p =>
            {
                var distance = MapGeometry.DistanceToPolyline(line, p, out var along);
                return distance < half && !InGap(gaps, along);
            });
        }

        private static void Plant(MapPlanContext context, MapBeltKit kit, List<Vector2> line, List<Vector2> gaps, int seed)
        {
            var random = context.Random(MapRandomChannel.BeltTrees, seed);
            var length = MapGeometry.Length(line);
            var path = new MapPathWalker(line);
            var rowGap = kit.rows > 1 ? (kit.width - 2f) / kit.rows : 0f;
            for (var row = 0; row < kit.rows; row++)
            {
                var offset = (row - (kit.rows - 1) * 0.5f) * rowGap;
                var start = random.NextDouble() * kit.spacing;
                for (var along = (float)start; along < length; along += kit.spacing * (1f + Jitter * (random.Value() - 0.5f)))
                {
                    if (!InGap(gaps, along))
                    {
                        PlantAt(context, kit, path, along, offset, random);
                    }
                }
            }
        }

        private static void PlantAt(MapPlanContext context, MapBeltKit kit, MapPathWalker path, float along,
            float offset, System.Random random)
        {
            var point = path.PointAt(along, out var direction);
            var side = MapGeometry.Right(direction);
            var shrub = !string.IsNullOrEmpty(kit.shrub) && random.NextDouble() < kit.shrubShare;
            var key = shrub ? kit.shrub : random.Pick(kit.trees, t => t.weight).tree;
            var jitter = (random.Value() - 0.5f) * 1.2f;
            var spot = point + side * (offset + jitter);
            if (!context.Layout.Symmetry.InSector(spot) || !context.Spacing.Fits(spot, MinSpacing))
            {
                return;
            }

            var tree = context.Climate.TreeIndex(key);
            context.PlantCopies(tree, spot, context.TreeScale(tree, random), random.Yaw());
        }

        /// <summary>Gap centres along the belt (x) and their half widths (y).</summary>
        private static List<Vector2> Gaps(MapBeltKit kit, float length, int seed, int index)
        {
            var gaps = new List<Vector2>();
            if (kit.gapEvery <= 0f)
            {
                return gaps;
            }

            var random = MapRandom.Stream(seed, MapRandomChannel.BeltGaps, index);
            for (var at = kit.gapEvery * (0.4f + 0.4f * random.Value()); at < length; at += kit.gapEvery * (0.7f + 0.6f * random.Value()))
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
