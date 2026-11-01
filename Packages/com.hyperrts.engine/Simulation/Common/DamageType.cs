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

        /// <summary>The garrison factor of <paramref name="type"/>, 1 for plain damage.</summary>
        public static float Garrison(DamageType type) => type != null ? type.garrisonFactor : 1f;
    }
}
