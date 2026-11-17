using UnityEngine;

namespace HyperRTS.Presentation.Effects.Rig
{
    /// <summary>A blinding flash a burst gives the screen: where it went off, how bright and how far it is seen.</summary>
    public readonly struct WhiteOutPulse
    {
        public readonly Vector3 Position;

        /// <summary>Share of the full white-out at the burst point, from 0 to 1.</summary>
        public readonly float Strength;

        /// <summary>Distance from the camera's focus at which it no longer shows.</summary>
        public readonly float Reach;

        public WhiteOutPulse(Vector3 position, float strength, float reach)
        {
            Position = position;
            Strength = strength;
            Reach = reach;
        }
    }
}
