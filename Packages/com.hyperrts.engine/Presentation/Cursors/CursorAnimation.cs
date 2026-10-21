using System;
using HyperRTS.Client.Interaction;
using UnityEngine;

namespace HyperRTS.Presentation.Cursors
{
    /// <summary>The animated cursor shown for one <see cref="CursorKind"/>, drawn at several sizes.</summary>
    [Serializable]
    public sealed class CursorAnimation
    {
        public CursorKind kind;

        [Tooltip("Frames per second.")]
        [Min(0.01f)]
        public float fps = 12f;

        [Tooltip("Repeat the frames, or hold the last one.")]
        public bool loop = true;

        [Tooltip("The same frames drawn at each pixel size.")]
        public CursorFrames[] sizes;

        /// <summary>The frame index shown <paramref name="elapsed"/> seconds after the cursor appeared.</summary>
        public int FrameAt(float elapsed, int count)
        {
            var frame = (int)(Mathf.Max(0f, elapsed) * fps);
            return loop ? frame % count : Mathf.Min(frame, count - 1);
        }

        /// <summary>The size closest to <paramref name="pixels"/>, the larger one on a tie.</summary>
        public CursorFrames Closest(float pixels)
        {
            var best = sizes[0];
            foreach (var each in sizes)
            {
                var gap = Mathf.Abs(each.size - pixels);
                var bestGap = Mathf.Abs(best.size - pixels);
                if (gap < bestGap || (gap == bestGap && each.size > best.size))
                {
                    best = each;
                }
            }

            return best;
        }
    }
}
