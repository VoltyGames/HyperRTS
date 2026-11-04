using HyperRTS.Simulation.Common;
using System;
using UnityEngine;

namespace HyperRTS.Simulation.Squads
{
    /// <summary>A match's cover rules as set on the Match; baked into <see cref="CoverRules"/>.</summary>
    [Serializable]
    public struct CoverSettings
    {
        [Tooltip("Share of damage taken behind heavy cover (sandbags, concrete, wrecks, rocks), from the front.")]
        [Range(0f, 1f)]
        public float heavyDamage;

        [Tooltip("Share of suppression taken behind heavy cover, from the front.")]
        [Range(0f, 1f)]
        public float heavySuppression;

        [Tooltip("Share of damage taken behind light cover (fences, hay, woodpiles), from the front.")]
        [Range(0f, 1f)]
        public float lightDamage;

        [Tooltip("Share of suppression taken behind light cover, from the front.")]
        [Range(0f, 1f)]
        public float lightSuppression;

        [Tooltip("Half angle in degrees either side of a spot's facing that its cover protects.")]
        [Range(0f, 180f)]
        public float frontArc;

        [Tooltip("Metres round a halted squad to look for spots, at most the squad's spread.")]
        [Min(0f)]
        public float searchRadius;

        public static CoverSettings Standard => new()
        {
            heavyDamage = 0.5f, heavySuppression = 0.3f, lightDamage = 0.75f, lightSuppression = 0.6f,
            frontArc = 75f, searchRadius = 3.5f,
        };

        public readonly CoverRules Rules => new()
        {
            HeavyDamage = heavyDamage,
            HeavySuppression = heavySuppression,
            LightDamage = lightDamage,
            LightSuppression = lightSuppression,
            ArcCos = Mathf.Cos(frontArc * Mathf.Deg2Rad),
            SearchRadius = searchRadius,
        };
    }
}
