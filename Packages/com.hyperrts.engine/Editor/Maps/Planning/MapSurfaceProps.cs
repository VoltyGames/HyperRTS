using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// Scatters one surface prop over a region's sector cells, or lines them up along its rows the way a baler leaves
    /// bales across a stubble field, clear of roads and everything placed.
    /// </summary>
    public static class MapSurfaceProps
    {
        private const int Attempts = 30;
        private const float RowSpacing = 16f;
        private const float AlongSpacing = 24f;
        private const float RowJitter = 2f;

        public static void Place(MapPlanContext context, MapLandRegion region, MapPropDensity props, List<int> cells,
            float hectares, System.Random random)
        {
            var entry = context.Palette.Entry(props.kind);
            var wanted = Mathf.RoundToInt(props.perHectare * hectares);
            if (props.alongRows)
            {
                InRows(context, region, entry, cells, wanted, random);
                return;
            }

            var placed = 0;
            for (var attempt = 0; attempt < wanted * Attempts && placed < wanted; attempt++)
            {
                var spot = MapSurfacePlanter.RandomSpot(context.Land, cells, random);
                placed += TryPlace(context, entry, spot, random.Yaw(), random) ? 1 : 0;
            }
        }

        private static void InRows(MapPlanContext context, MapLandRegion region, MapPaletteEntry entry,
            List<int> cells, int wanted, System.Random random)
        {
            var land = context.Land;
            var forward = MapGeometry.Forward(region.RowAngle);
            var right = MapGeometry.Right(forward);
            var bounds = Bounds(land, cells);
            var reach = bounds.size.magnitude * 0.5f;
            var center = bounds.center;
            var phase = random.Value() * RowSpacing;
            var placed = 0;
            for (var across = -reach + phase; across < reach && placed < wanted; across += RowSpacing)
            {
                for (var along = -reach + random.Value() * AlongSpacing; along < reach && placed < wanted; along += AlongSpacing)
                {
                    var spot = center + right * across + forward * along + right * Jitter(random) + forward * Jitter(random);
                    var inRegion = land.At(spot) == region;
                    if (inRegion && context.Layout.Symmetry.InSector(spot))
                    {
                        var yaw = region.RowAngle + 90f + Jitter(random) * 4f;
                        placed += TryPlace(context, entry, spot, yaw, random) ? 1 : 0;
                    }
                }
            }
        }

        private static bool TryPlace(MapPlanContext context, MapPaletteEntry entry, Vector2 spot, float yaw,
            System.Random random)
        {
            var scale = random.Range(entry.scale.x, entry.scale.y);
            var radius = entry.Radius * scale;
            var clearance = context.Placer.Clearance;
            if (!clearance.IsOffRoad(spot, radius) || !clearance.IsFree(spot, radius))
            {
                return false;
            }

            if (!context.Spacing.Fits(spot, radius + 1f))
            {
                return false;
            }

            context.Placer.AddEverywhere(entry, spot, yaw, scale);
            return true;
        }

        private static Rect Bounds(MapLand land, List<int> cells)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var cell in cells)
            {
                var center = land.Center(cell % land.Width, cell / land.Width);
                min = Vector2.Min(min, center);
                max = Vector2.Max(max, center);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static float Jitter(System.Random random) => random.Signed() * RowJitter;
    }
}
