using HyperRTS.Core;
using UnityEngine;

namespace HyperRTS.Simulation.Common
{
    /// <summary>A damage category (bullet, explosive, flame) that armor scales; leave unset for plain damage.</summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Combat + "Damage Type", fileName = "DamageType")]
    [HelpURL(HyperRTSDocs.Combat)]
    public class DamageType : ScriptableObject
    {
        [Tooltip("Name shown in tooltips.")]
        public string displayName = "Bullet";

        [Tooltip("Share of a hit on a building that reaches its garrison past the walls: 0.5 = half, above 1 for blast and fire that fill rooms.")]
        [Min(0f)]
        public float garrisonFactor = 1f;

        [Tooltip("Share of a soldier's cover that a hit ignores: 0 = full protection, 1 = none (thermobaric blasts fill the hole).")]
        [Range(0f, 1f)]
        public float coverPiercing;

        /// <summary>The cover piercing of <paramref name="type"/>, 0 for plain damage.</summary>
        public static float Piercing(DamageType type) => type != null ? type.coverPiercing : 0f;

        /// <summary>The garrison factor of <paramref name="type"/>, 1 for plain damage.</summary>
        public static float Garrison(DamageType type) => type != null ? type.garrisonFactor : 1f;
    }
}
