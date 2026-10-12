using HyperRTS.Simulation.Common;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Where delivery carriers enter the map: deterministic, from positions alone.</summary>
    public static class DeliveryMath
    {
        /// <summary>
        /// Direction from the map centre through <paramref name="position"/> on XZ; a building at the exact centre
        /// uses its own back (<paramref name="rotation"/>) instead.
        /// </summary>
        public static float2 Outward(in MapSettings map, float3 position, quaternion rotation)
        {
            var offset = position.xz - map.Center.xz;
            if (math.lengthsq(offset) > 1e-6f)
            {
                return math.normalize(offset);
            }

            return -math.normalize(math.mul(rotation, math.forward()).xz);
        }

        /// <summary>The playable-bounds point on the ray from the map centre along <paramref name="outward"/>.</summary>
        public static float3 EdgePoint(in MapSettings map, float2 outward)
        {
            var center = map.Center.xz;
            var toMax = (map.Max - center) / outward;
            var toMin = (map.Min - center) / outward;
            var exits = math.max(toMax, toMin);
            var point = center + outward * math.cmin(exits);
            return map.Clamp(new float3(point.x, 0f, point.y));
        }

        /// <summary>Carrier <paramref name="index"/> of a <paramref name="count"/>-wide wave, spread across the ray.</summary>
        public static float3 WavePosition(in MapSettings map, float3 edge, float2 outward, int index, int count,
            float spacing)
        {
            var side = new float2(-outward.y, outward.x) * ((index - (count - 1) * 0.5f) * spacing);
            return map.Clamp(edge + new float3(side.x, 0f, side.y));
        }
    }
}
