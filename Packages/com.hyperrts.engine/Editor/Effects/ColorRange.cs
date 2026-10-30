using System;
using UnityEngine;

namespace HyperRTS.Editor.Effects
{
    /// <summary>A start colour picked per particle between min and max, in linear HDR values.</summary>
    [Serializable]
    public struct ColorRange
    {
        public Color min;
        public Color max;
    }
}
