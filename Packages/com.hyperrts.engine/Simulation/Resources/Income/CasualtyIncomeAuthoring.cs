using HyperRTS.Simulation.Common.Authoring;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Resources.Income
{
    /// <summary>Pays the owner a share of the cost of every own or hostile unit that dies while this entity is online.</summary>
    [AddComponentMenu(HyperRTSMenu.Entity + "Resources/Casualty Income")]
    [Icon(HyperRTSIcons.CasualtyIncomeAuthoring)]
    [HelpURL(HyperRTSDocs.Resources)]
    [DisallowMultipleComponent]
    public class CasualtyIncomeAuthoring : AuthoringBehaviour
    {
        [Tooltip("Resource paid, taken from the fallen unit's cost of the same type.")]
        public ResourceType type;

        [Tooltip("Fraction of that cost paid per death, rounded down.")]
        [Range(0.01f, 1f)]
        public float share = 0.1f;

        public class Baker : Baker<CasualtyIncomeAuthoring>
        {
            public override void Bake(CasualtyIncomeAuthoring authoring)
            {
                var writer = this.Writer();
                writer.Add(new CasualtyIncome { Type = authoring.type, Share = authoring.share });
            }
        }
    }
}
