using System;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>
    /// How strongly the terrain's macro map varies a climate's ground (<see cref="Ground.MapMacro"/>): each field's and
    /// wood's own tint, a slow gradient across it, soil patches within it, and darker, damper hollows. Light and warmth
    /// are shifts of the neutral 0.5 macro colour.
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

        [Tooltip("Metres across the largest soil patches; the noise adds patches down to an eighth of that.")]
        public float patchWavelength;

        [Tooltip("Light swing of the soil patches.")]
        public float patchLight;

        [Tooltip("Warmth swing of the soil patches.")]
        public float patchWarmth;

        [Tooltip("Wetness (0..1) of the dampest soil patches.")]
        public float patchWetness;

        /// <summary>Every field by name, each of which must be above 0.</summary>
        public (string Field, float Value)[] Fields() => new[]
        {
            ("regionLight", regionLight), ("regionWarmth", regionWarmth), ("gradientLight", gradientLight),
            ("gradientWarmth", gradientWarmth), ("gradientWavelength", gradientWavelength),
            ("hollowDarkening", hollowDarkening), ("hollowWetness", hollowWetness),
            ("patchWavelength", patchWavelength), ("patchLight", patchLight), ("patchWarmth", patchWarmth),
            ("patchWetness", patchWetness),
        };
    }
}
