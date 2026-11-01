using System;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>One damage type and how much of it a unit's armor or a building's section material takes.</summary>
    [Serializable]
    public struct DamageFactor
    {
        [Tooltip("Damage type this entry applies to.")]
        public DamageType damageType;

        [Tooltip("Damage multiplier: 0.25 = takes a quarter, 2 = takes double.")]
        [Min(0f)]
        public float multiplier;
    }
}
