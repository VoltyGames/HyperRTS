using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>One planted tree: climate prototype, map position, scale and yaw in degrees.</summary>
    public readonly struct MapTree
    {
        public MapTree(int prototype, Vector2 position, float scale, float yaw)
        {
            Prototype = prototype;
            Position = position;
            Scale = scale;
            Yaw = yaw;
        }

        public int Prototype { get; }

        public Vector2 Position { get; }

        public float Scale { get; }

        public float Yaw { get; }
    }
}
