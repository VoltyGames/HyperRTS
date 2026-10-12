using System.Collections.Generic;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Spec;
using HyperRTS.Editor.Maps.Symmetry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Refine
{
    /// <summary>
    /// Smooths authored ravines into curves and grows their side gullies: shorter, narrower, shallower branches
    /// leaving the main cut at an angle, the way balkas fork. Branches stay in the authored sector.
    /// </summary>
    public static class MapRavineRefiner
    {
        private const float Step = 4f;
        private const float MinBranch = 25f;

        /// <summary>Refines the spec's ravines in place and appends their branches.</summary>
        public static void Refine(MapSpec spec, MapSymmetry symmetry)
        {
            var branches = new List<MapRavineSpec>();
            for (var i = 0; i < spec.ravines.Count; i++)
            {
                var ravine = spec.ravines[i];
                var curve = MapCurves.Spline(MapGeometry.ToVectors(ravine.points), Step);
                ravine.points = MapGeometry.ToPoints(curve);
                var random = new System.Random(unchecked(spec.seed * 31337 + i * 7907));
                for (var b = 0; b < ravine.branches; b++)
                {
                    var branch = Branch(ravine, curve, random, symmetry);
                    if (branch != null)
                    {
                        branches.Add(branch);
                    }
                }
            }

            spec.ravines.AddRange(branches);
        }

        private static MapRavineSpec Branch(MapRavineSpec main, List<Vector2> curve, System.Random random,
            MapSymmetry symmetry)
        {
            var length = MapGeometry.Length(curve);
            var start = MapGeometry.PointAlong(curve, length * Mathf.Lerp(0.2f, 0.8f, (float)random.NextDouble()), out var direction);
            var side = random.NextDouble() < 0.5 ? -1f : 1f;
            var angle = Mathf.Lerp(35f, 65f, (float)random.NextDouble()) * side;
            var heading = MapGeometry.Rotate(direction, angle);
            var reach = Mathf.Max(MinBranch, length * Mathf.Lerp(0.3f, 0.55f, (float)random.NextDouble()));
            var bend = MapGeometry.Rotate(heading, -angle * 0.35f);
            var middle = start + heading * (reach * 0.5f);
            var end = middle + bend * (reach * 0.5f);
            var points = MapCurves.Spline(new List<Vector2> { start, middle, end }, Step);
            if (!points.TrueForAll(symmetry.InSector))
            {
                return null;
            }

            return new MapRavineSpec
            {
                points = MapGeometry.ToPoints(points),
                width = main.width * 0.6f,
                depth = main.depth * 0.65f,
                rim = main.rim * 0.7f,
                wooded = main.wooded,
            };
        }
    }
}
