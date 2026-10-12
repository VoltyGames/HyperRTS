using System.Collections.Generic;
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
        private const float WoodedReach = 0.75f;
        private const float PatchSize = 28f;
        private const float ThresholdStep = 2f;

        public static void Plan(MapPlanContext context)
        {
            if (context.Shapes.Ravines.Count == 0)
            {
                return;
            }

            var climate = context.Climate;
            var cut = context.Region(MapLandKind.Ravine, climate.ravineSurface);
            var rim = context.Region(MapLandKind.RavineRim, climate.ravineRimSurface);
            var wobble = new MapNoise(context.Layout.Symmetry, context.Seed, MapRavineWall.WobbleChannel);
            foreach (var (line, spec) in context.Shapes.Ravines)
            {
                var half = spec.width * 0.5f;
                var outer = half + spec.rim;
                context.Land.ClaimEach(line.Bounds, p =>
                {
                    var distance = MapRavineWall.Distance(line, wobble, p);
                    if (distance < half)
                    {
                        return cut;
                    }

                    return distance < outer ? rim : MapLand.Unclaimed;
                });
            }

            PlantWoods(context, wobble);
        }

        /// <summary>
        /// The patch noise level that leaves <paramref name="wooded"/> of this ravine's floor above it: noise averaged
        /// over the symmetry's copies has a narrow, shifting range, so a fixed cut would wood one balka and bare another.
        /// </summary>
        private static float Threshold(MapLine line, MapNoise patches, float reach, float wooded)
        {
            var samples = new List<float>();
            for (var along = 0f; along <= line.Length; along += ThresholdStep)
            {
                var point = line.Path.PointAt(along, out var direction);
                var across = new Vector2(-direction.y, direction.x);
                for (var side = -1f; side <= 1f; side += 0.5f)
                {
                    samples.Add(patches.Sample(point + across * (reach * side), PatchSize));
                }
            }

            samples.Sort();
            var index = Mathf.Clamp(Mathf.FloorToInt((1f - wooded) * samples.Count), 0, samples.Count - 1);
            return samples[index];
        }

        /// <summary>A random point on the ravine's floor and lower walls, up to <paramref name="reach"/> off its line.</summary>
        private static Vector2 OnFloor(MapLine line, float reach, System.Random random)
        {
            var along = (float)random.NextDouble() * line.Length;
            var point = line.Path.PointAt(along, out var direction);
            var across = new Vector2(-direction.y, direction.x);
            return point + across * (reach * ((float)random.NextDouble() * 2f - 1f));
        }

        private static void PlantWoods(MapPlanContext context, MapNoise wobble)
        {
            var surface = context.Climate.Surface(context.Climate.ravineSurface);
            if (surface.trees.Count == 0)
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
                var threshold = Threshold(line, patches, reach, spec.wooded);
                var random = Placement.MapPlacer.Random(context.Seed, 24, ravine.Index);
                MapTreeScatter.Scatter(context, surface.trees, hectares, surface.treeSpacing, random,
                    () => OnFloor(line, reach, random), p =>
                    {
                        var inSector = context.Layout.Symmetry.InSector(p);
                        var onFloor = MapRavineWall.Distance(line, wobble, p) < reach;
                        return inSector && onFloor && patches.Sample(p, PatchSize) > threshold;
                    });
            }
        }
    }
}
