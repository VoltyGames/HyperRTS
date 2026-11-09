using System;
using HyperRTS.Editor.Maps.Library;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Climate
{
    /// <summary>
    /// How strongly the ground tint map (<see cref="Ground.MapGroundTint"/>) varies a climate's ground.
    /// </summary>
    [Serializable]
    public sealed class MapClimateGroundTint
    {
        [Tooltip("Range of each field's and wood's own light shift, centred on none.")]
        [Above(0f)]
        public float regionLight;

        [Tooltip("Range of each field's and wood's own warmth shift (red up, blue down), centred on none.")]
        [Above(0f)]
        public float regionWarmth;

        [Tooltip("Light swing of the slow gradient across a field or wood.")]
        [Above(0f)]
        public float gradientLight;

        [Tooltip("Warmth swing of the slow gradient across a field or wood.")]
        [Above(0f)]
        public float gradientWarmth;

        [Tooltip("Metres per cycle of that gradient; longer than a field, so a field shows a fraction of one.")]
        [Above(0f)]
        public float gradientLength;

        [Tooltip("Light taken off a clear hollow.")]
        [Above(0f)]
        public float hollowDarkening;

        [Tooltip("Wetness (0..1) of a clear hollow.")]
        [Above(0f)]
        public float hollowWetness;

        [Tooltip("Metres across the largest soil colour patches; the noise adds smaller ones down to an eighth of that.")]
        [Above(0f)]
        public float soilSize;

        [Tooltip("Light swing of the soil colour.")]
        [Above(0f)]
        public float soilLight;

        [Tooltip("Warmth swing of the soil colour.")]
        [Above(0f)]
        public float soilWarmth;

        [Tooltip("Wetness (0..1) of the dampest soil.")]
        [Above(0f)]
        public float soilWetness;

        [Tooltip("Light taken off a full drainage line.")]
        [Above(0f)]
        public float flowDarkening;

        [Tooltip("Wetness (0..1) of a full drainage line.")]
        [Above(0f)]
        public float flowWetness;

        [Tooltip("Green shift of a full drainage line.")]
        [Above(0f)]
        public float flowGreening;

        [Tooltip("Metres of wet, dark, glossy ground along a river's edge (1 to 2).")]
        [Above(0f)]
        public float bankWidth;

        [Tooltip("Wetness (0..1) at the water's edge, fading to nothing over the bank width.")]
        [Above(0f)]
        public float bankWetness;

        [Tooltip("Metres of fainter damp ground beyond the bank band, from the water's edge (5 to 10).")]
        [Above(0f)]
        public float dampWidth;

        [Tooltip("Wetness (0..1) of the damp at the water's edge, fading to nothing over the damp width.")]
        [Above(0f)]
        public float dampWetness;
    }
}
