using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using UnityEngine;

namespace HyperRTS.Simulation.Destruction
{
    /// <summary>What a building section is made of (brick, timber, glass), and how much each damage type takes out of it.</summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Destruction + "Section Material", fileName = "SectionMaterial")]
    [HelpURL(HyperRTSDocs.Destruction)]
    public class SectionMaterial : ScriptableObject
    {
        [Tooltip("Name shown in tooltips.")]
        public string displayName = "Brick";

        [Tooltip("Per-type multipliers; unlisted types deal full damage.")]
        public List<DamageFactor> factors = new();
    }
}
