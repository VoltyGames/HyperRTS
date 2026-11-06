using System;
using UnityEngine;

namespace HyperRTS.Presentation.Animation.Clips
{
    /// <summary>One clip of a <see cref="ClipTable"/>, as it is saved in the table asset and the bake info file.</summary>
    [Serializable]
    public class NamedClip
    {
        [Tooltip("Name game code plays the clip by.")]
        public string name;

        [Tooltip("The clip's first texture row.")]
        [Min(0)]
        public int startRow;

        [Tooltip("Rows in the clip. A loop does not repeat its first row at the end.")]
        [Min(1)]
        public int frameCount = 1;

        [Tooltip("Rows per second.")]
        [Min(0.01f)]
        public float fps = 30f;

        [Tooltip("Loops; off plays once and holds the last row.")]
        public bool loop = true;

        [Tooltip("Metres per second the clip's travel covers, which a bone clip baked in place no longer shows; 0 for none, so it plays at its own rate.")]
        [Min(0f)]
        public float groundSpeed;

        public ClipRange Range => new()
        {
            StartRow = startRow, FrameCount = frameCount, Fps = fps, Loop = loop, GroundSpeed = groundSpeed,
        };
    }
}
