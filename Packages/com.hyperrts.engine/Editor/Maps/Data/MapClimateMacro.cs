using System;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>
    /// How strongly the terrain's macro map varies a climate's ground (<see cref="Ground.MapMacro"/>): each field's and
    /// wood's own tint, a slow gradient across it, and darker, damper hollows. Light and warmth are shifts of the
    /// neutral 0.5 macro colour.
    /// </summary>
    [Serializable]
    public sealed class MapClimateMacro
    {
        [Tooltip("Range of each field's and wood's own light shift, centred on none.")]
        public float regionLight;

        [Tooltip("Range of each field's and wood's own warmth shift (red up, blue down), centred on none.")]
        public float regionWarmth;

        [Tooltip("Light swing of the slow gradient across a field or wood.")]
        public float gradientLight;

        [Tooltip("Warmth swing of the slow gradient across a field or wood.")]
        public float gradientWarmth;

        [Tooltip("Metres per cycle of that gradient; longer than a field, so a field shows a fraction of one.")]
        public float gradientWavelength;

        [Tooltip("Light taken off a clear hollow.")]
        public float hollowDarkening;

        [Tooltip("Wetness (0..1) of a clear hollow.")]
        public float hollowWetness;
    }
}
