using System;
using UnityEngine;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>What high ground is worth in a match, as set on the Match; baked into <see cref="HighGroundRules"/>.</summary>
    [Serializable]
    public struct HighGroundSettings
    {
        [Tooltip("Metres around a spot whose mean ground it is measured against.")]
        [Min(2f)]
        public float radius;

        [Tooltip("Share of sight range gained per metre a viewer stands above the ground around it.")]
        [Min(0f)]
        public float sightPerMetre;

        [Tooltip("Most sight range high ground adds, as a share.")]
        [Min(0f)]
        public float maxSight;

        [Tooltip("Share of range a direct-fire weapon gains per metre its unit stands above the target.")]
        [Min(0f)]
        public float rangePerMetre;

        [Tooltip("Most range height adds to direct fire, as a share.")]
        [Min(0f)]
        public float maxRange;

        public static HighGroundSettings Standard => new()
        {
            radius = 50f, sightPerMetre = 0.02f, maxSight = 0.25f, rangePerMetre = 0.01f, maxRange = 0.1f,
        };

        public readonly HighGroundRules Rules => new()
        {
            Radius = radius,
            SightPerMetre = sightPerMetre,
            MaxSight = maxSight,
            RangePerMetre = rangePerMetre,
            MaxRange = maxRange,
        };
    }
}
