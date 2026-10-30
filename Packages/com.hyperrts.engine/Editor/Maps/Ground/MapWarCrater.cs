using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>An old shell crater, grassed over: a soft bowl with a low rim, in map space.</summary>
    public readonly struct MapWarCrater
    {
        /// <summary>Rim height as a share of the bowl's depth; old rims have slumped.</summary>
        private const float RimShare = 0.15f;

        /// <summary>How far past the bowl's radius the rim spreads, as a share of the radius.</summary>
        private const float RimReach = 0.5f;

        public MapWarCrater(Vector2 center, float radius, float depth)
        {
            Center = center;
            Radius = radius;
            Depth = depth;
        }

        public Vector2 Center { get; }

        public float Radius { get; }

        public float Depth { get; }

        /// <summary>Distance from the centre past which the ground is untouched.</summary>
        public float Reach => Radius * (1f + RimReach);

        /// <summary>Height change at a distance from the centre: down in the bowl, slightly up on the rim.</summary>
        public float Offset(float distance)
        {
            var t = distance / Radius;
            var bowl = t < 1f ? Square(1f - t * t) : 0f;
            var rim = Mathf.Max(0f, 1f - Mathf.Abs(t - 1f) / RimReach);
            return Depth * (RimShare * rim * rim - bowl);
        }

        public void DigInto(MapHeights heights)
        {
            var crater = this;
            heights.ForEachNear(Center, Reach, (x, z, distance) => heights[x, z] += crater.Offset(distance));
        }

        /// <summary>Share of the plants kept at a distance: <paramref name="bottom"/> in the middle, all at the rim.</summary>
        public float Plants(float distance, float bottom) => Mathf.Lerp(bottom, 1f, Mathf.Clamp01(distance / Radius));

        /// <summary>Weed cover of the ring just outside the rim, peaking halfway across it.</summary>
        public float Weeds(float distance, float ring)
        {
            var across = (distance - Radius) / ring;
            return across is < 0f or > 1f ? 0f : Mathf.Sin(across * Mathf.PI);
        }

        private static float Square(float value) => value * value;
    }
}
