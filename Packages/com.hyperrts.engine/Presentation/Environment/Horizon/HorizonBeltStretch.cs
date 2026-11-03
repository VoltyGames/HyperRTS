using UnityEngine;

namespace HyperRTS.Presentation.Environment.Horizon
{
    /// <summary>One unbroken stretch of horizon belt: its centre line in the world and the strip material it draws.</summary>
    public struct HorizonBeltStretch
    {
        public Vector2 from;
        public Vector2 to;

        /// <summary>Index into <see cref="HorizonBelts.strips"/>.</summary>
        public int strip;
    }
}
