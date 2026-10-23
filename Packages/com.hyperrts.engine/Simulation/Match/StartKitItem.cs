using System;
using UnityEngine;

namespace HyperRTS.Simulation.Match
{
    /// <summary>One piece of a <see cref="SideContent"/> start kit.</summary>
    [Serializable]
    public class StartKitItem
    {
        [Tooltip("Unit or building prefab.")]
        public GameObject prefab;

        [Tooltip("Metres right (x) and forward (y) of the spawn.")]
        public Vector2 offset;

        [Tooltip("Degrees, added to the spawn's yaw.")]
        public float yaw;

        [Tooltip("Offset and yaw face the match camera, as a placed building does, instead of turning with the spawn.")]
        public bool facesCamera;
    }
}
