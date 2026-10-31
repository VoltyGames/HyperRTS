using System.Collections.Generic;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// Shelterbelts and hedgerows: a strip of ground along a polyline with rows of trees, shrubs in the gaps and on
    /// the edges, and breaks where tracks cut through. Plans the spec's belts; farmland lays its field-end belts
    /// through <see cref="PlanLine"/>.
    /// </summary>
    public sealed class MapBeltPlanner : MapLandPass
    {
        private const float Jitter = 0.25f;
        private const float MinSpacing = 2f;

        public MapBeltPlanner(MapPlanContext context) : base(context)
        {
        }

        public override void Plan()
        {
            foreach (var belt in Context.Layout.Belts)
            {
                var kit = Climate.BeltKit(belt.Placed.kind);
                var line = MapGeometry.ToVectors(belt.Placed.points);
                var gaps = Gaps(kit, MapGeometry.Length(line), belt.Index);
                Claim(kit, line, gaps, belt.Index);
                if (belt.Copy == 0)
                {
                    Plant(kit, line, gaps, belt.Index);
                }
            }
        }

        /// <summary>A belt along an authored-sector polyline, claimed and planted in every copy.</summary>
        public void PlanLine(MapBeltKit kit, List<Vector2> sectorLine, int seed)
        {
            var gaps = Gaps(kit, MapGeometry.Length(sectorLine), seed);
            foreach (var copy in Symmetry.Copies)
            {
                Claim(kit, sectorLine.ConvertAll(copy.Apply), gaps, seed);
            }

            Plant(kit, sectorLine, gaps, seed);
        }

        private void Claim(MapBeltKit kit, List<Vector2> line, List<Vector2> gaps, int seed)
        {
            var region = Context.Region(MapLandKind.Belt, kit.surface, seed);
            var half = kit.width * 0.5f;
            Land.ClaimWhere(MapGeometry.Bounds(line, half + 1f), region, p =>
            {
                var distance = MapGeometry.DistanceToPolyline(line, p, out var along);
                return distance < half && !InGap(gaps, along);
            });
        }

        private void Plant(MapBeltKit kit, List<Vector2> line, List<Vector2> gaps, int seed)
        {
            var random = Context.Random(MapRandomChannel.BeltTrees, seed);
            var length = MapGeometry.Length(line);
            var path = new MapLineWalker(line);
            var rowGap = kit.rows > 1 ? (kit.width - 2f) / kit.rows : 0f;
            for (var row = 0; row < kit.rows; row++)
            {
                var offset = (row - (kit.rows - 1) * 0.5f) * rowGap;
                var start = random.NextDouble() * kit.spacing;
                for (var along = (float)start; along < length; along += kit.spacing * (1f + Jitter * (random.Value() - 0.5f)))
                {
                    if (!InGap(gaps, along))
                    {
                        PlantAt(kit, path, along, offset, random);
                    }
                }
            }
        }

        private void PlantAt(MapBeltKit kit, MapLineWalker path, float along, float offset, System.Random random)
        {
            var point = path.PointAt(along, out var direction);
            var shrub = !string.IsNullOrEmpty(kit.shrub) && random.NextDouble() < kit.shrubShare;
            var key = shrub ? kit.shrub : random.Pick(kit.trees, t => t.weight).tree;
            var jitter = (random.Value() - 0.5f) * 1.2f;
            var spot = point + MapGeometry.Right(direction) * (offset + jitter);
            if (Symmetry.InSector(spot) && Context.Trees.Fits(spot, MinSpacing))
            {
                Context.Trees.Plant(Climate.TreeIndex(key), spot, random);
            }
        }

        /// <summary>Gap centres along the belt (x) and their half widths (y).</summary>
        private List<Vector2> Gaps(MapBeltKit kit, float length, int index)
        {
            var gaps = new List<Vector2>();
            if (kit.gapEvery <= 0f)
            {
                return gaps;
            }

            var random = Context.Random(MapRandomChannel.BeltGaps, index);
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
