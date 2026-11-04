using HyperRTS.Core;
using HyperRTS.Simulation.Common.Authoring;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Squads
{
    /// <summary>
    /// Marks a structure or vehicle where friendly squads bring back dead members with the Reinforce command, while
    /// they stand within its range and it is working.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Reinforce Point")]
    [HelpURL(HyperRTSDocs.Combat)]
    [DisallowMultipleComponent]
    public class ReinforcePointAuthoring : AuthoringBehaviour
    {
        [Tooltip("Metres from this object within which a squad can be reinforced.")]
        [Min(1f)]
        public float range = 25f;

        public class Baker : Baker<ReinforcePointAuthoring>
        {
            public override void Bake(ReinforcePointAuthoring authoring) =>
                this.Writer().Add(new ReinforcePoint { Range = authoring.range });
        }
    }
}
