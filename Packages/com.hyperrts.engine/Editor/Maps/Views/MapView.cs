using UnityEngine;

namespace HyperRTS.Editor.Maps.Views
{
    /// <summary>One standard screenshot of a map: where the camera looks, from how high, and its file name.</summary>
    public readonly struct MapView
    {
        public MapView(string name, Vector2 focus, float height, float yaw = 0f)
        {
            Name = name;
            Focus = focus;
            Height = height;
            Yaw = yaw;
        }

        public string Name { get; }

        /// <summary>The ground point (map XZ) in the middle of the picture.</summary>
        public Vector2 Focus { get; }

        public float Height { get; }

        public float Yaw { get; }
    }
}
