using System.Collections.Generic;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Palette;
using HyperRTS.Editor.Maps.Placement;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning.Settlements
{
    /// <summary>
    /// Sections of one prop (fence sections, gates, pipes) laid end to end between two points. An entry's long
    /// side is its length, whichever axis the prefab built it on.
    /// </summary>
    internal static class MapSections
    {
        private const float MinLength = 0.5f;

        /// <summary>Length of one section along the run.</summary>
        public static float Length(MapProp entry) => Mathf.Max(MinLength, entry.size.x, entry.size.y);

        /// <summary>The yaw that lays an entry's long side along a direction.</summary>
        public static float YawAlong(MapProp entry, Vector2 direction)
        {
            var yaw = MapGeometry.Yaw(direction);
            return entry.size.y >= entry.size.x ? yaw : yaw + 90f;
        }

        /// <summary>Centres of the whole sections that fit end to end between two points, centred between them.</summary>
        public static List<Vector2> Centers(MapProp entry, Vector2 from, Vector2 to)
        {
            var centers = new List<Vector2>();
            var span = to - from;
            var length = span.magnitude;
            var section = Length(entry);
            if (length < section * 0.5f)
            {
                return centers;
            }

            var direction = span / length;
            var count = Mathf.Max(1, Mathf.RoundToInt(length / section));
            var first = (length - count * section) * 0.5f + section * 0.5f;
            for (var i = 0; i < count; i++)
            {
                centers.Add(from + direction * (first + section * i));
            }

            return centers;
        }

        /// <summary>Places the sections in every copy of the sector.</summary>
        public static void Place(MapPlacements placements, MapProp entry, IEnumerable<Vector2> centers,
            Vector2 direction)
        {
            var yaw = YawAlong(entry, direction);
            foreach (var center in centers)
            {
                placements.AddEverywhere(entry, center, yaw, 1f, 1f);
            }
        }
    }
}
