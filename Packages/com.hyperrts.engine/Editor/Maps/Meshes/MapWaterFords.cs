using System.Collections.Generic;
using HyperRTS.Editor.Maps.Geometry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Meshes
{
    /// <summary>River vertex colour at fords: A thins the water over the shallow bed, G marks fast water for foam.</summary>
    public static class MapWaterFords
    {
        /// <summary>Water alpha mid-ford, where the bed is barely submerged.</summary>
        public const float FordAlpha = 0.25f;

        /// <summary>Metres past a ford's edge over which the water deepens again.</summary>
        private const float Fade = 3f;

        /// <summary>Metres of fast water downstream of a ford.</summary>
        private const float FastRun = 10f;

        /// <summary>Colour at a river centre-line <paramref name="point"/> flowing towards <paramref name="downstream"/>.</summary>
        public static Color At(IReadOnlyList<MapCrossing> crossings, Vector2 point, Vector2 downstream)
        {
            var shallow = 0f;
            var fast = 0f;
            foreach (var crossing in crossings)
            {
                if (crossing.IsBridge)
                {
                    continue;
                }

                var local = crossing.Local(point);
                if (Mathf.Abs(local.y) > crossing.Span * 0.5f)
                {
                    continue;
                }

                var edge = Mathf.Abs(local.x) - crossing.Width * 0.5f;
                var below = Vector2.Dot(point - crossing.Position, downstream) > 0f;
                shallow = Mathf.Max(shallow, 1f - MapGeometry.Smooth(edge / Fade));
                fast = Mathf.Max(fast, 1f - MapGeometry.Smooth(edge / (below ? FastRun : Fade)));
            }

            return new Color(0f, fast, 0f, Mathf.Lerp(1f, FordAlpha, shallow));
        }
    }
}
