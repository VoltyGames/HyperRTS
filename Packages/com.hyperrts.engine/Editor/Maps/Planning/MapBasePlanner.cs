using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Placement;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// The ground of each base, a worn rectangular yard turned with its spawn, and the pads under props whose entry
    /// asks for one.
    /// </summary>
    public sealed class MapBasePlanner : MapLandPass
    {
        /// <summary>A yard's half width and half depth as shares of the base radius: corners stay where it flattens.</summary>
        private static readonly Vector2 YardHalf = new(0.86f, 0.69f);

        /// <summary>How far a yard's edge wanders in and out, and how round its corners wear, as shares of the radius.</summary>
        private const float YardWander = 0.04f;
        private const float YardCorner = 0.18f;
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
                    Yard(ground, spawn, Context.Palette.baseRadius, noise);
                }
            }
        }

        private void Yard(int region, MapSpawn spawn, float radius, MapNoise noise)
        {
            var half = YardHalf * radius;
            var corner = YardCorner * radius;
            var wander = YardWander * radius;
            var forward = MapGeometry.Forward(spawn.Yaw);
            var right = MapGeometry.Right(forward);
            var center = spawn.Position;
            var reach = half.magnitude + wander;
            var area = new Rect(center - Vector2.one * reach, Vector2.one * (reach * 2f));
            Land.ClaimWhere(area, region, p =>
            {
                var offset = p - center;
                var local = new Vector2(Mathf.Abs(Vector2.Dot(offset, right)), Mathf.Abs(Vector2.Dot(offset, forward)));
                // A rounded rectangle: distance outside the box shrunk by the corner radius.
                var outside = Vector2.Max(local - (half - Vector2.one * corner), Vector2.zero).magnitude - corner;
                return outside + noise.Sample(p, BaseWavelength) * wander < 0f;
            });
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
