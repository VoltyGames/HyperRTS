using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>Every feature list of a map spec, in drawing order (areas under lines under points).</summary>
    public static class MapFeatureKinds
    {
        public static readonly IReadOnlyList<MapFeatureKind> All = new[]
        {
            MapFeatureKind.Polygon("farmland", "Farmland", new Color(0.85f, 0.75f, 0.3f)),
            MapFeatureKind.Polygon("areas", "Areas", new Color(0.55f, 0.8f, 0.4f)),
            MapFeatureKind.Polygon("woods", "Woods", new Color(0.15f, 0.55f, 0.2f)),
            MapFeatureKind.Polygon("scatters", "Scatters", new Color(0.9f, 0.6f, 0.6f)),
            MapFeatureKind.Circle("hills", "Hills", new Color(0.75f, 0.6f, 0.35f), "pos", "radius"),
            MapFeatureKind.Circle("settlements", "Settlements", new Color(0.95f, 0.45f, 0.3f), "center", "radius"),
            MapFeatureKind.Polyline("rivers", "Rivers", new Color(0.25f, 0.5f, 0.95f)),
            MapFeatureKind.Polyline("ravines", "Ravines", new Color(0.6f, 0.4f, 0.25f)),
            MapFeatureKind.Polyline("belts", "Belts", new Color(0.35f, 0.7f, 0.3f)),
            MapFeatureKind.Polyline("lines", "Lines", new Color(0.8f, 0.5f, 0.9f)),
            MapFeatureKind.Polyline("roads", "Roads", new Color(0.92f, 0.9f, 0.85f)),
            MapFeatureKind.Point("props", "Props", new Color(0.85f, 0.8f, 0.7f)),
            MapFeatureKind.Point("resources", "Resources", new Color(1f, 0.85f, 0.2f)),
            MapFeatureKind.Point("spawns", "Spawns", new Color(0.2f, 0.6f, 1f)),
        };

        public static MapFeatureKind Find(string field)
        {
            foreach (var kind in All)
            {
                if (kind.Field == field)
                {
                    return kind;
                }
            }

            throw new ArgumentException($"No map feature kind for spec list '{field}'.");
        }
    }
}
