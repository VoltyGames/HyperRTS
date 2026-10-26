using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// Ravines (balkas): the steep cut and the grassy rim slope round it, and woods in groves on the floor and lower
    /// walls, as much of it as the spec's <c>wooded</c> share says — never filled to the brim.
    /// </summary>
    public sealed class MapRavinePlanner : MapLandStep
    {
        private const float WoodedReach = 0.75f;
        private const float GroveSize = 28f;
        private const float ThresholdStep = 2f;

        public MapRavinePlanner(MapPlanContext context) : base(context)
        {
        }

        public override void Plan()
        {
            if (Context.Shapes.Ravines.Count == 0)
            {
                return;
            }

            var cut = Context.Region(MapLandKind.Ravine, Climate.ravineSurface);
            var rim = Context.Region(MapLandKind.RavineRim, Climate.ravineRimSurface);
            foreach (var ravine in Context.Shapes.Ravines)
            {
                var half = ravine.Spec.width * 0.5f;
                var outer = half + ravine.Spec.rim;
                Land.ClaimEach(ravine.Line.Bounds, p =>
                {
                    var distance = ravine.WallDistance(p);
                    if (distance < half)
                    {
                        return cut;
                    }

                    return distance < outer ? rim : MapLand.Unclaimed;
                });
            }

            PlantWoods();
        }

        private void PlantWoods()
        {
            var surface = Climate.Surface(Climate.ravineSurface);
            if (surface.trees.Count == 0)
            {
                return;
            }

            var groves = Context.Noise(MapNoiseChannel.RavineWoods);
            foreach (var ravine in Context.Layout.Ravines.Where(r => r.Copy == 0 && r.Authored.wooded > 0f))
            {
                PlantWood(ravine.Authored, ravine.Index, surface, groves);
            }
        }

        private void PlantWood(MapRavineSpec spec, int index, MapClimateSurface surface, MapNoise groves)
        {
            var line = new MapLine(MapGeometry.ToVectors(spec.points), spec.width, spec.width * 0.5f);
            var floor = new MapRavine(line, spec, Context.Shapes.EdgeWander);
            var reach = spec.width * 0.5f * WoodedReach;
            var hectares = line.Length * reach * 2f * spec.wooded / 10000f;
            var threshold = Threshold(line, groves, reach, spec.wooded);
            var random = Context.Random(MapRandomChannel.RavineTrees, index);
            Context.Trees.Scatter(surface.trees, hectares, surface.treeSpacing, random,
                () => OnFloor(line, reach, random), p =>
                {
                    var inSector = Symmetry.InSector(p);
                    var onFloor = floor.WallDistance(p) < reach;
                    return inSector && onFloor && groves.Sample(p, GroveSize) > threshold;
                });
        }

        /// <summary>
        /// The grove noise level that leaves <paramref name="wooded"/> of this ravine's floor above it: noise averaged
        /// over the symmetry's copies has a narrow, shifting range, so a fixed cut would wood one balka and bare another.
        /// </summary>
        private static float Threshold(MapLine line, MapNoise groves, float reach, float wooded)
        {
            var samples = new List<float>();
            for (var along = 0f; along <= line.Length; along += ThresholdStep)
            {
                var point = line.Path.PointAt(along, out var direction);
                var across = MapGeometry.Left(direction);
                for (var side = -1f; side <= 1f; side += 0.5f)
                {
                    samples.Add(groves.Sample(point + across * (reach * side), GroveSize));
                }
            }

            samples.Sort();
            var index = Mathf.Clamp(Mathf.FloorToInt((1f - wooded) * samples.Count), 0, samples.Count - 1);
            return samples[index];
        }

        /// <summary>A random point on the ravine's floor and lower walls, up to <paramref name="reach"/> off its line.</summary>
        private static Vector2 OnFloor(MapLine line, float reach, System.Random random)
        {
            var along = random.Value() * line.Length;
            var point = line.Path.PointAt(along, out var direction);
            var across = MapGeometry.Left(direction);
            return point + across * (reach * random.Signed());
        }
    }
}
