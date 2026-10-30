using System;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>
    /// How strongly the terrain's ground tint map varies a climate's ground (<see cref="Ground.MapGroundTint"/>): each field's and
    /// wood's own tint, a slow gradient across it, soil colour, hollows and drainage lines. Light, warmth and green
    /// shift the neutral 0.5 tint.
    /// </summary>
    [Serializable]
    public sealed class MapClimateGroundTint
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
        public float gradientLength;

        [Tooltip("Light taken off a clear hollow.")]
        public float hollowDarkening;

        [Tooltip("Wetness (0..1) of a clear hollow.")]
        public float hollowWetness;

        [Tooltip("Metres across the largest soil colour patches; the noise adds smaller ones down to an eighth of that.")]
        public float soilSize;

        [Tooltip("Light swing of the soil colour.")]
        public float soilLight;

        [Tooltip("Warmth swing of the soil colour.")]
        public float soilWarmth;

        [Tooltip("Wetness (0..1) of the dampest soil.")]
        public float soilWetness;

        [Tooltip("Light taken off a full drainage line.")]
        public float flowDarkening;

        [Tooltip("Wetness (0..1) of a full drainage line.")]
        public float flowWetness;

        [Tooltip("Green shift of a full drainage line.")]
        public float flowGreening;

        /// <summary>Every field by name, each of which must be above 0.</summary>
        public (string Field, float Value)[] Fields() => new[]
        {
            ("regionLight", regionLight), ("regionWarmth", regionWarmth), ("gradientLight", gradientLight),
            ("gradientWarmth", gradientWarmth), ("gradientLength", gradientLength),
            ("hollowDarkening", hollowDarkening), ("hollowWetness", hollowWetness),
            ("soilSize", soilSize), ("soilLight", soilLight), ("soilWarmth", soilWarmth),
            ("soilWetness", soilWetness), ("flowDarkening", flowDarkening), ("flowWetness", flowWetness),
            ("flowGreening", flowGreening),
        };
    }
}
