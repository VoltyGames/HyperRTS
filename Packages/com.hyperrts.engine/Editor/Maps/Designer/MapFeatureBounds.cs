using System.Collections.Generic;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>The area a feature covers in metres, padded so framing it shows its surroundings too.</summary>
    public static class MapFeatureBounds
    {
        private const float Padding = 40f;

        public static Rect Of(MapSpec spec, MapFeatureRef feature)
        {
            var points = new List<Vector2>();
            feature.Kind.ReadPoints(spec, feature.Index, points);
            if (points.Count == 0)
            {
                // Farmland without points fills the free land: the whole playable area.
                var size = spec.size.ToVector2();
                return new Rect(-size * 0.5f, size);
            }

            var reach = feature.Kind.Radius(spec, feature.Index) + Padding;
            var min = points[0];
            var max = points[0];
            foreach (var point in points)
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            return Rect.MinMaxRect(min.x - reach, min.y - reach, max.x + reach, max.y + reach);
        }
    }
}
