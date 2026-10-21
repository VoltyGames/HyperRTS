using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Placement;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// The ground round each base, with a wandering edge so it never reads as a stamped circle, and the pads (gravel,
    /// concrete, trampled dirt) under the props whose entry asks for one: a rectangle or oval turned with the prop,
    /// its edge wandering by the entry's pad wander.
    /// </summary>
    public sealed class MapBasePlanner : MapLandStep
    {
        /// <summary>How far a base's edge wanders in and out, as a share of its radius.</summary>
        private const float BaseWander = 0.18f;
        private const float BaseWavelength = 22f;
        private const float PadWavelength = 6f;

        public MapBasePlanner(MapPlanContext context) : base(context)
        {
        }

        public override void Plan()
        {
            var noise = Context.Noise(MapNoiseChannel.BaseEdges);
            // Pads go first so a prop inside a base, such as its supply field, still gets its own ground.
            foreach (var placement in Context.Placements.All)
            {
                var pad = placement.Entry.pad;
                if (!string.IsNullOrEmpty(pad))
                {
                    Pad(Context.Region(MapLandKind.Pad, pad), placement, noise);
                }
            }

            var spawns = Context.Layout.Spawns;
            if (spawns.Count > 0)
            {
                var ground = Context.Region(MapLandKind.Base, Climate.baseSurface);
                foreach (var spawn in spawns)
                {
                    Base(ground, spawn.Position, Context.Palette.baseRadius, noise);
                }
            }
        }

        private void Base(int region, Vector2 center, float radius, MapNoise noise)
        {
            var wander = radius * BaseWander;
            var reach = radius + wander;
            var area = new Rect(center - Vector2.one * reach, Vector2.one * (reach * 2f));
            Land.ClaimWhere(area, region, p => (p - center).magnitude + noise.Sample(p, BaseWavelength) * wander < radius);
        }

        private void Pad(int region, MapPlacement placement, MapNoise noise)
        {
            var entry = placement.Entry;
            var half = entry.size * 0.5f + Vector2.one * entry.padMargin;
            var forward = MapGeometry.Forward(placement.Yaw);
            var right = MapGeometry.Right(forward);
            var reach = half.magnitude + entry.padWander;
            var center = placement.Position;
            var area = new Rect(center - Vector2.one * reach, Vector2.one * (reach * 2f));
            Land.ClaimWhere(area, region, p =>
            {
                var offset = p - center;
                var edge = noise.Sample(p, PadWavelength) * entry.padWander;
                var across = Mathf.Abs(Vector2.Dot(offset, right)) + edge;
                var along = Mathf.Abs(Vector2.Dot(offset, forward)) + edge;
                return entry.padRound
                    ? new Vector2(across / half.x, along / half.y).sqrMagnitude < 1f
                    : across < half.x && along < half.y;
            });
        }
    }
}
