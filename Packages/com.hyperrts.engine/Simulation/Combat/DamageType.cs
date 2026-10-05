using HyperRTS.Core;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>A damage category (bullet, explosive, flame) that armor scales; leave unset for plain damage.</summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Combat + "Damage Type", fileName = "DamageType")]
    [HelpURL(HyperRTSDocs.Modules)]
    public class DamageType : ScriptableObject
    {
        [Tooltip("Name shown in tooltips.")]
        public string displayName = "Bullet";
    }
}
