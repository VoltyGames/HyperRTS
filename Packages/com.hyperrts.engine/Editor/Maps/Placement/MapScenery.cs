using UnityEngine;

namespace HyperRTS.Editor.Maps.Placement
{
    /// <summary>A prop in the backdrop outside the play area, drawn on the terrain from its visual-only model.</summary>
    public readonly struct MapScenery
    {
        public MapScenery(GameObject model, Vector2 position, float yaw, float scale, float radius)
        {
            Model = model;
            Position = position;
            Yaw = yaw;
            Scale = scale;
            Radius = radius;
        }

        public GameObject Model { get; }

        public Vector2 Position { get; }

        /// <summary>Degrees.</summary>
        public float Yaw { get; }

        public float Scale { get; }

        /// <summary>Scaled footprint radius (metres), so scenery keeps apart.</summary>
        public float Radius { get; }
    }
}
