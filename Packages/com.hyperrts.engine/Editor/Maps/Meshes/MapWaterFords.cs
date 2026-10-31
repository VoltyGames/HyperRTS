using System.Collections.Generic;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Geometry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Meshes
{
    /// <summary>
    /// River vertex colour at crossings: A is how shallow a ford's bed is, G marks a ford's fast water and R the foam
    /// lines trailing downstream of fords and bridges, all from the climate's ford and crossing foam distances.
    /// </summary>
    public static class MapWaterFords
    {
        /// <summary>Colour at a river centre-line <paramref name="point"/> flowing towards <paramref name="downstream"/>.</summary>
        public static Color VertexColor(IReadOnlyList<MapCrossing> crossings, MapClimate climate, Vector2 point, Vector2 downstream)
        {
            var (shallow, fast, trail) = (0f, 0f, 0f);
            foreach (var crossing in crossings)
            {
                var local = crossing.Local(point);
                if (Mathf.Abs(local.y) > crossing.Span * 0.5f)
                {
                    continue;
                }

                var edge = Mathf.Abs(local.x) - crossing.Width * 0.5f;
                var below = Vector2.Dot(point - crossing.Position, downstream) > 0f;
                if (below && climate.crossingFoamLength > 0f)
                {
                    trail = Mathf.Max(trail, 1f - MapGeometry.Smooth(edge / climate.crossingFoamLength));
                }

                if (crossing.IsBridge)
                {
                    continue;
                }

                var run = below ? climate.fordRapidsLength : climate.fordShallowsWidth;
                shallow = Mathf.Max(shallow, 1f - MapGeometry.Smooth(edge / climate.fordShallowsWidth));
                fast = Mathf.Max(fast, 1f - MapGeometry.Smooth(edge / run));
            }

            return new Color(trail, fast, 0f, shallow);
        }
    }
}
