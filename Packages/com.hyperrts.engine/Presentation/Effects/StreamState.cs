using UnityEngine;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>One stream's path and leftover particle fraction between frames.</summary>
    public sealed class StreamState
    {
        /// <summary>Last frame's point; this frame's on the first frame, so the first segment is empty.</summary>
        public Vector3 From;

        public Vector3 To;
        public float Carry;
        public int Frame;
    }
}
