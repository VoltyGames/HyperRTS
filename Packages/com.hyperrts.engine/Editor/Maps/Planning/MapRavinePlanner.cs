using System.Linq;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// Ravines (balkas): the steep cut and the grassy rim slope round it, and woods in patches on the floor and lower
    /// walls, as much of it as the spec's <c>wooded</c> share says — never filled to the brim.
    /// </summary>
    public static class MapRavinePlanner
    {
        /// <summary>The height shaper's wall wobble, so land use follows the carved shape.</summary>
        private const float Wobble = 1.5f;

        private const int WobbleChannel = 1;
        private const float WoodedReach = 0.75f;

        public static void Plan(MapPlanContext context)
        {
            if (context.Shapes.Ravines.Count == 0)
            {
                return;
            }

            var climate = context.Climate;
            var cut = context.Region(MapLandKind.Ravine, climate.ravineSurface, "ravine");
            var rim = context.Region(MapLandKind.RavineRim, climate.ravineRimSurface, "ravine rim");
            var wobble = new MapNoise(context.Layout.Symmetry, context.Seed, WobbleChannel);
            foreach (var (line, spec) in context.Shapes.Ravines)
            {
                var half = spec.width * 0.5f;
                context.Land.ClaimWhere(line.Bounds, cut, p => Distance(line, wobble, p) < half);
                context.Land.ClaimWhere(line.Bounds, rim, p => Distance(line, wobble, p) < half + spec.rim);
            }

            PlantWoods(context, wobble);
        }

        private static float Distance(MapLine line, MapNoise wobble, Vector2 p) =>
            line.Distance(p) + wobble.Sample(p, 15f) * Wobble;

        private static void PlantWoods(MapPlanContext context, MapNoise wobble)
        {
            var surface = context.Climate.Surface(context.Climate.ravineSurface);
            if (surface == null || surface.trees.Count == 0)
            {
                return;
            }

            var patches = new MapNoise(context.Layout.Symmetry, context.Seed, 23);
            foreach (var ravine in context.Layout.Ravines.Where(r => r.Copy == 0))
            {
                var spec = ravine.Authored;
                if (spec.wooded <= 0f)
                {
                    continue;
                }

                var line = new MapLine(MapGeometry.ToVectors(spec.points), spec.width, spec.width * 0.5f);
                var reach = spec.width * 0.5f * WoodedReach;
                var hectares = line.Length * reach * 2f * spec.wooded / 10000f;
                var threshold = 1f - 2f * spec.wooded;
                var random = Placement.MapPlacer.Random(context.Seed, 24, ravine.Index);
                MapTreeScatter.Scatter(context, surface.trees, hectares, surface.treeSpacing, random,
                    () => MapTreeScatter.In(line.Bounds, random), p =>
                    {
                        var inSector = context.Layout.Symmetry.InSector(p);
                        var onFloor = Distance(line, wobble, p) < reach;
                        return inSector && onFloor && patches.Sample(p, 28f) > threshold;
                    }, $"ravine {ravine.Index + 1}");
            }
        }
    }
}
