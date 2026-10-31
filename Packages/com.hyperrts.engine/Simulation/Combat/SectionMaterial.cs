using System;
using System.Collections.Generic;
using HyperRTS.Core;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>What a building section is made of (brick, timber, glass), and how much each damage type takes out of it.</summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Combat + "Section Material", fileName = "SectionMaterial")]
    [HelpURL(HyperRTSDocs.Combat)]
    public class SectionMaterial : ScriptableObject
    {
        [Serializable]
        public struct Factor
        {
            [Tooltip("Damage type this entry applies to.")]
            public DamageType damageType;

            [Tooltip("Damage multiplier: 0.25 = takes a quarter, 2 = takes double.")]
            [Min(0f)]
            public float multiplier;
        }

        [Tooltip("Name shown in tooltips.")]
        public string displayName = "Brick";

        [Tooltip("Per-type multipliers; unlisted types deal full damage.")]
        public List<Factor> factors = new();
    }
}
