using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// Pieces of one palette entry (fence sections, gates, pipes) laid end to end along a run. An entry's long side
    /// is its length, whichever axis the prefab built it on.
    /// </summary>
    internal static class MapPieceRun
    {
        private const float MinLength = 0.5f;

        /// <summary>Length of one piece along a run.</summary>
        public static float Length(MapPaletteEntry entry) => Mathf.Max(MinLength, entry.size.x, entry.size.y);

        /// <summary>The yaw that lays an entry's long side along a direction.</summary>
        public static float YawAlong(MapPaletteEntry entry, Vector2 direction)
        {
            var yaw = MapGeometry.Yaw(direction);
            return entry.size.y >= entry.size.x ? yaw : yaw + 90f;
        }

        /// <summary>Centres of the whole pieces that fit end to end between two points, centred on the run.</summary>
        public static List<Vector2> Centers(MapPaletteEntry entry, Vector2 from, Vector2 to)
        {
            var centers = new List<Vector2>();
            var span = to - from;
            var length = span.magnitude;
            var piece = Length(entry);
            if (length < piece * 0.5f)
            {
                return centers;
            }

            var direction = span / length;
            var count = Mathf.Max(1, Mathf.RoundToInt(length / piece));
            var first = (length - count * piece) * 0.5f + piece * 0.5f;
            for (var i = 0; i < count; i++)
            {
                centers.Add(from + direction * (first + piece * i));
            }

            return centers;
        }

        /// <summary>Places the pieces in every copy of the sector.</summary>
        public static void Place(MapPlanContext context, MapPaletteEntry entry, IEnumerable<Vector2> centers,
            Vector2 direction)
        {
            var yaw = YawAlong(entry, direction);
            foreach (var center in centers)
            {
                context.Placer.AddEverywhere(entry, center, yaw, 1f);
            }
        }
    }
}
