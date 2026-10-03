using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Hud
{
    /// <summary>Maps between world XZ and normalized minimap space (0..1, y pointing down like UI).</summary>
    public static class MinimapMath
    {
        public static Vector2 WorldToMinimap(float2 world, float2 mapMin, float2 mapSize)
        {
            var uv = (world - mapMin) / mapSize;
            return new Vector2(uv.x, 1f - uv.y);
        }

        public static float2 MinimapToWorld(Vector2 point, float2 mapMin, float2 mapSize) =>
            mapMin + new float2(point.x, 1f - point.y) * mapSize;

        /// <summary>Where a ray meets the plane y = height, capped at maxDistance (rays above the horizon included).</summary>
        public static Vector3 GroundPoint(Ray ray, float height, float maxDistance)
        {
            var direction = ray.direction;
            var distance = direction.y < -1e-4f ? (height - ray.origin.y) / direction.y : maxDistance;
            var point = ray.GetPoint(Mathf.Clamp(distance, 0f, maxDistance));
            return new Vector3(point.x, height, point.z);
        }
    }
}
