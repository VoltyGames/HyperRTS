using HyperRTS.Core;
using HyperRTS.Simulation.Common.Authoring;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Squads
{
    /// <summary>
    /// Marks a structure or vehicle squads retreat to. The Retreat command sends a squad to the nearest friendly one
    /// that is working, and the retreat ends when the squad is within the radius.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Retreat Point")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Combat)]
    [DisallowMultipleComponent]
    public class RetreatPointAuthoring : AuthoringBehaviour
    {
        [Tooltip("Metres from this object within which a retreating squad is safe.")]
        [Min(1f)]
        public float radius = 8f;

        public class Baker : Baker<RetreatPointAuthoring>
        {
            public override void Bake(RetreatPointAuthoring authoring) =>
                this.Writer().Add(new RetreatPoint { Radius = authoring.radius });
        }
    }
}
