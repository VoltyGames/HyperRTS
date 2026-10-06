using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.HUD
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
    }
}
