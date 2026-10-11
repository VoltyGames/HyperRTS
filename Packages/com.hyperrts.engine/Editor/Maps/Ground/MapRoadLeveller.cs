using HyperRTS.Editor.Maps.Geometry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>
    /// Levels each road onto a smoothed height profile taken from the ground before ravines were cut, so a road
    /// crosses a ravine on an embankment. River channels under the road are left alone (fords and bridges).
    /// </summary>
    public static class MapRoadLeveller
    {
        private const float Step = 2f;
        private const int SmoothingSteps = 3;
        private const float EndTaper = 8f;
        private const int SmoothingPasses = 2;
        private const float AboveWater = 0.6f;

        public static void Level(MapHeights heights, MapHeights open, MapShapes shapes, float water)
        {
            foreach (var road in shapes.Roads)
            {
                var profile = Profile(road, open, water);
                heights.ForEachIn(road.Bounds, (x, z, p) =>
                {
                    var distance = road.Distance(p, out var along);
                    var edge = distance - road.Width * 0.5f;
                    if (edge > MapShapes.RoadShoulder || shapes.RiverEdgeDistance(p) < 1f)
                    {
                        return;
                    }

                    // Roads ease out at their ends instead of stopping on a levelled terrace.
                    var ends = MapGeometry.Smooth(Mathf.Min(along, road.Length - along) / EndTaper);
                    var weight = (1f - MapGeometry.Smooth(edge / MapShapes.RoadShoulder)) * ends;
                    heights[x, z] = Mathf.Lerp(heights[x, z], At(profile, along), weight);
                });
            }
        }

        private static float[] Profile(MapLine road, MapHeights open, float water)
        {
            var count = Mathf.CeilToInt(road.Length / Step) + 1;
            var profile = new float[count];
            for (var i = 0; i < count; i++)
            {
                profile[i] = open.Height(MapGeometry.PointAlong(road.Points, i * Step, out _));
            }

            for (var pass = 0; pass < SmoothingPasses; pass++)
            {
                profile = Smooth(profile);
            }

            for (var i = 0; i < count; i++)
            {
                profile[i] = Mathf.Max(profile[i], water + AboveWater);
            }

            return profile;
        }

        private static float[] Smooth(float[] profile)
        {
            var smoothed = new float[profile.Length];
            for (var i = 0; i < profile.Length; i++)
            {
                var sum = 0f;
                var count = 0;
                for (var j = Mathf.Max(0, i - SmoothingSteps); j <= Mathf.Min(profile.Length - 1, i + SmoothingSteps); j++)
                {
                    sum += profile[j];
                    count++;
                }

                smoothed[i] = sum / count;
            }

            return smoothed;
        }

        private static float At(float[] profile, float along)
        {
            var index = Mathf.Clamp(along / Step, 0f, profile.Length - 1);
            var low = Mathf.Min((int)index, profile.Length - 2);
            return profile.Length == 1 ? profile[0] : Mathf.Lerp(profile[low], profile[low + 1], index - low);
        }
    }
}
