using System;
using UnityEngine;

namespace HyperRTS.Presentation.Cursors
{
    /// <summary>One cursor drawn at one pixel size: its animation frames and the hotspot they share.</summary>
    [Serializable]
    public sealed class CursorFrames
    {
        [Tooltip("Width and height in pixels.")]
        public int size;

        [Tooltip("Pixel the click lands on, from the top-left corner.")]
        public Vector2 hotspot;

        [Tooltip("Readable, uncompressed cursor textures in playback order.")]
        public Texture2D[] frames;
    }
}
