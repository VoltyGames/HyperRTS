using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Meshes
{
    /// <summary>
    /// River vertex colour at fords: A is how shallow the bed is (the water material thins it), G marks fast water
    /// for foam. Distances come from the climate's <see cref="MapClimate.fordFade"/> and <see cref="MapClimate.fordFastRun"/>.
    /// </summary>
    public static class MapWaterFords
    {
        /// <summary>Colour at a river centre-line <paramref name="point"/> flowing towards <paramref name="downstream"/>.</summary>
        public static Color At(IReadOnlyList<MapCrossing> crossings, MapClimate climate, Vector2 point, Vector2 downstream)
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
                var run = below ? climate.fordFastRun : climate.fordFade;
                shallow = Mathf.Max(shallow, 1f - MapGeometry.Smooth(edge / climate.fordFade));
                fast = Mathf.Max(fast, 1f - MapGeometry.Smooth(edge / run));
            }

            return new Color(0f, fast, 0f, shallow);
        }
    }
}
