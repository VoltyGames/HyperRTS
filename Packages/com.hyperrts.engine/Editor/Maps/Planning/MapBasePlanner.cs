using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// The ground round each base, and the pads (gravel, concrete) under the props whose palette entry asks for one,
    /// with a noisy edge so neither reads as a stamped circle.
    /// </summary>
    public static class MapBasePlanner
    {
        private const float EdgeNoise = 3f;

        public static void Plan(MapPlanContext context)
        {
            var noise = new MapNoise(context.Layout.Symmetry, context.Seed, 22);
            if (context.Layout.Spawns.Count > 0)
            {
                var radius = context.Palette.baseRadius;
                var ground = context.Region(MapLandKind.Base, context.Climate.baseSurface, "base ground");
                foreach (var spawn in context.Layout.Spawns)
                {
                    Disc(context, ground, spawn.Position, radius, noise);
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
                Disc(context, region, placement.Position, placement.Radius + placement.Entry.padMargin, noise);
            }
        }

        private static void Disc(MapPlanContext context, int region, Vector2 center, float radius, MapNoise noise)
        {
            var reach = radius + EdgeNoise;
            var area = new Rect(center - Vector2.one * reach, Vector2.one * (reach * 2f));
            context.Land.ClaimWhere(area, region, p => (p - center).magnitude + noise.Sample(p, 12f) * EdgeNoise < radius);
        }
    }
}
