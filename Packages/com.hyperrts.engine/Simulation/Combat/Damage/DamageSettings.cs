using HyperRTS.Simulation.Common.Infantry;
using System;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using UnityEngine;

namespace HyperRTS.Simulation.Combat.Damage
{
    /// <summary>The inspector fields of a <see cref="DamageProfile"/>, shared by weapons, abilities and bombs.</summary>
    [Serializable]
    public class DamageSettings
    {
        [Tooltip("Damage per hit before armor; negative heals.")]
        public float damage = 10f;

        [Tooltip("Optional damage category that armor can scale.")]
        public DamageType damageType;

        [Tooltip("Suppression one hit adds to a squad it lands on (1 pins it at once): machine guns most, rifles some. Splash adds it per soldier reached, shared out by the squad's size.")]
        [Range(0f, 1f)]
        public float suppression;

        [Tooltip("Radius around the impact that also takes damage; 0 hits only the target.")]
        [Min(0f)]
        public float radius;

        [Tooltip("Fraction of the damage dealt at the splash edge (1 = no falloff).")]
        [Range(0f, 1f)]
        [ShowIf(nameof(radius))]
        public float edgeFactor = 0.5f;

        [Tooltip("Splash also hurts the source's own and allied units.")]
        [ShowIf(nameof(radius))]
        public bool friendlyFire;

        public DamageProfile Bake() => new()
        {
            Amount = damage,
            Type = damageType,
            GarrisonFactor = DamageType.Garrison(damageType),
            CoverPiercing = DamageType.Piercing(damageType),
            Suppression = suppression,
            Radius = radius,
            EdgeFactor = edgeFactor,
            FriendlyFire = friendlyFire,
        };
    }
}
