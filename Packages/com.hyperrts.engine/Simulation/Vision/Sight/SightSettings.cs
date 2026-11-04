using HyperRTS.Simulation.Vision.Detection;
using System;
using UnityEngine;

namespace HyperRTS.Simulation.Vision.Sight
{
    /// <summary>A match's sight rules as set on the Match; baked into <see cref="SightRules"/>.</summary>
    [Serializable]
    public struct SightSettings
    {
        [Tooltip("Metres around a spot whose mean ground its height is measured against.")]
        [Min(2f)]
        public float highGroundRadius;

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

        [Tooltip("Concealed infantry show to enemy ground units this close, in metres.")]
        [Min(0f)]
        public float revealDistance;

        public static SightSettings Standard => new()
        {
            highGroundRadius = 50f, sightPerMetre = 0.02f, maxSight = 0.25f, rangePerMetre = 0.01f, maxRange = 0.1f,
            revealDistance = 15f,
        };

        public readonly SightRules Rules => new()
        {
            Radius = highGroundRadius,
            SightPerMetre = sightPerMetre,
            MaxSight = maxSight,
            RangePerMetre = rangePerMetre,
            MaxRange = maxRange,
            RevealDistance = revealDistance,
        };
    }
}
