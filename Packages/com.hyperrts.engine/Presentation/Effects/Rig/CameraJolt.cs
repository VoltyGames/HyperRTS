using UnityEngine;

namespace HyperRTS.Presentation.Effects.Rig
{
    /// <summary>A shake a burst gives the camera: where it went off and how hard.</summary>
    public readonly struct CameraJolt
    {
        public readonly Vector3 Position;

        /// <summary>Metres of camera offset at the burst point at the closest zoom.</summary>
        public readonly float Strength;

        public CameraJolt(Vector3 position, float strength)
        {
            Position = position;
            Strength = strength;
        }
    }
}
