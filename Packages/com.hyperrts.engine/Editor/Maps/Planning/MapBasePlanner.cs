using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Placement;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// The ground round each base, with a wandering edge so it never reads as a stamped circle, and the pads (gravel,
    /// concrete) under the props whose palette entry asks for one: a rectangle turned with the prop, its edge broken a
    /// little, the way a yard is laid out.
    /// </summary>
    public static class MapBasePlanner
    {
        /// <summary>How far a base's edge wanders in and out, as a share of its radius.</summary>
        private const float BaseWander = 0.18f;
        private const float BaseWavelength = 22f;
        private const float PadNoise = 0.8f;

        public static void Plan(MapPlanContext context)
        {
            var noise = new MapNoise(context.Layout.Symmetry, context.Seed, 22);
            if (context.Layout.Spawns.Count > 0)
            {
                var radius = context.Palette.baseRadius;
                var ground = context.Region(MapLandKind.Base, context.Climate.baseSurface, "base ground");
                foreach (var spawn in context.Layout.Spawns)
                {
                    Base(context, ground, spawn.Position, radius, noise);
                }
            }

            foreach (var placement in context.Placer.Placements)
            {
                var pad = placement.Entry.pad;
                if (string.IsNullOrEmpty(pad))
                {
                    continue;
                }

                var region = context.Region(MapLandKind.Pad, pad, $"pad of '{placement.Entry.key}'");
                Pad(context, region, placement, noise);
            }
        }

        private static void Base(MapPlanContext context, int region, Vector2 center, float radius, MapNoise noise)
        {
            var wander = radius * BaseWander;
            var reach = radius + wander;
            var area = new Rect(center - Vector2.one * reach, Vector2.one * (reach * 2f));
            context.Land.ClaimWhere(area, region,
                p => (p - center).magnitude + noise.Sample(p, BaseWavelength) * wander < radius);
        }

        private static void Pad(MapPlanContext context, int region, MapPlacement placement, MapNoise noise)
        {
            var half = placement.Entry.size * 0.5f + Vector2.one * placement.Entry.padMargin;
            var forward = MapGeometry.Forward(placement.Yaw);
            var right = new Vector2(forward.y, -forward.x);
            var reach = half.magnitude + PadNoise;
            var center = placement.Position;
            var area = new Rect(center - Vector2.one * reach, Vector2.one * (reach * 2f));
            context.Land.ClaimWhere(area, region, p =>
            {
                var offset = p - center;
                var edge = noise.Sample(p, 6f) * PadNoise;
                var across = Mathf.Abs(Vector2.Dot(offset, right)) + edge;
                var along = Mathf.Abs(Vector2.Dot(offset, forward)) + edge;
                return across < half.x && along < half.y;
            });
        }
    }
}
