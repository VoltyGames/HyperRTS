using UnityEngine;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>An authored burst with a start time, waiting to fire at the pose of the burst that queued it.</summary>
    public struct DelayedBurst
    {
        public float Remaining;
        public int Emitter;
        public int Count;
        public Vector3 Position;
        public Quaternion Rotation;
        public float Scale;
    }
}
