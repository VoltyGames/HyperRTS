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
        [Tooltip("Name shown in tooltips.")]
        public string displayName = "Brick";

        [Tooltip("Per-type multipliers; unlisted types deal full damage.")]
        public List<DamageFactor> factors = new();
    }
}
