using HyperRTS.Simulation.Common.Infantry;
using System;
using UnityEngine;

namespace HyperRTS.Simulation.Squads.Cover
{
    /// <summary>What one kind of cover does for a soldier, as set in <see cref="CoverSettings"/>; baked into a <see cref="CoverEntry"/>.</summary>
    [Serializable]
    public struct CoverProtection
    {
        [Tooltip("Share of damage taken behind this cover, from the front.")]
        [Range(0f, 1f)]
        public float damageTaken;

        [Tooltip("Share of suppression taken behind this cover, from the front.")]
        [Range(0f, 1f)]
        public float suppressionTaken;

        [Tooltip("How much a squad wants a spot of this kind, scored against the metres from its place in the ring: a gap of 10 outweighs 10 m of walking.")]
        [Min(0f)]
        public float preference;

        public readonly CoverEntry Entry => new()
        {
            DamageTaken = damageTaken, SuppressionTaken = suppressionTaken, Preference = preference,
        };
    }
}
