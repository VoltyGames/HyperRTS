using System.Collections.Generic;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Refine
{
    /// <summary>
    /// Turns an authored river polyline into a natural channel: a smooth curve with meanders either side, held
    /// straight at its ends and crossings so copies join up and bridges span a straight reach.
    /// </summary>
    public static class MapRiverRefiner
    {
        private const float Step = 4f;
        private const float MeanderWavelength = 95f;
        private const float HoldReach = 40f;

        public static void Refine(MapRiverSpec river, int seed, int index)
        {
            var authored = MapGeometry.ToVectors(river.points);
            if (authored.Count < 2)
            {
                return;
            }

            var curve = MapCurves.Spline(authored, Step);
            var holds = Holds(river, authored, curve);
            var meandered = Meander(curve, river.meander, holds, seed, index);
            var refined = MapCurves.Smooth(meandered, 2);
            RemapCrossings(river, authored, refined);
            river.points = MapGeometry.ToPoints(refined);
        }

        private static List<Vector2> Meander(List<Vector2> curve, float amplitude, List<float> holds, int seed, int index)
        {
            if (amplitude <= 0f)
            {
                return curve;
            }

            var random = new System.Random(unchecked(seed * 92821 + index * 68903));
            var phase = (float)random.NextDouble() * 1000f;
            var meandered = new List<Vector2>(curve.Count);
            var along = 0f;
            for (var i = 0; i < curve.Count; i++)
            {
                along += i > 0 ? (curve[i] - curve[i - 1]).magnitude : 0f;
                var ahead = curve[Mathf.Min(i + 1, curve.Count - 1)] - curve[Mathf.Max(i - 1, 0)];
                var normal = new Vector2(-ahead.y, ahead.x).normalized;
                var wave = Mathf.PerlinNoise(phase + along / MeanderWavelength, 0.37f) * 2f - 1f;
                meandered.Add(curve[i] + normal * (wave * amplitude * Hold(along, holds)));
            }

            return meandered;
        }

        /// <summary>Where the meander is held, as distances along the spline: both ends and every crossing.</summary>
        private static List<float> Holds(MapRiverSpec river, List<Vector2> authored, List<Vector2> curve)
        {
            var length = MapGeometry.Length(authored);
            var holds = new List<float> { 0f, MapGeometry.Length(curve) };
            foreach (var crossing in river.crossings)
            {
                var place = MapGeometry.PointAlong(authored, crossing.t * length, out _);
                MapGeometry.DistanceToPolyline(curve, place, out var along);
                holds.Add(along);
            }

            return holds;
        }

        /// <summary>0 at the ends and crossings, easing to 1 away from them.</summary>
        private static float Hold(float along, List<float> holds)
        {
            var weight = 1f;
            foreach (var hold in holds)
            {
                weight *= MapGeometry.Smooth(Mathf.Abs(along - hold) / HoldReach);
            }

            return weight;
        }

        /// <summary>Each crossing keeps its place: its new t is the refined river's nearest point to the old one.</summary>
        private static void RemapCrossings(MapRiverSpec river, List<Vector2> authored, List<Vector2> refined)
        {
            var oldLength = MapGeometry.Length(authored);
            var newLength = MapGeometry.Length(refined);
            foreach (var crossing in river.crossings)
            {
                var place = MapGeometry.PointAlong(authored, crossing.t * oldLength, out _);
                MapGeometry.DistanceToPolyline(refined, place, out var along);
                crossing.t = Mathf.Clamp01(along / newLength);
            }
        }
    }
}
