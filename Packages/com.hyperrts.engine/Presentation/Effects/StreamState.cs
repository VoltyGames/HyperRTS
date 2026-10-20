using UnityEngine;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>One stream's path and leftover particle fraction between frames.</summary>
    public sealed class StreamState
    {
        /// <summary>Last frame's point; this frame's on the first ready frame, so the first segment is empty.</summary>
        public Vector3 From;

        public Vector3 To;
        public float Carry;
        public int Frame;
        public int Started;

        /// <summary>
        /// False on a stream's first frame, which only warms it up: an entity spawned through a command buffer still
        /// has its prefab's baked pose at the world origin that frame.
        /// </summary>
        public bool Ready => Frame != Started;
    }
}
