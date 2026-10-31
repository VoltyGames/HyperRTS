using System;
using UnityEngine;

namespace HyperRTS.Editor.Effects
{
    /// <summary>One colour key of a gradient, time 0 to 1.</summary>
    [Serializable]
    public struct ColorKey
    {
        public float time;
        [ColorUsage(true, true)] public Color color;
    }
}
