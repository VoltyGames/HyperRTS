using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Match
{
    /// <summary>
    /// The match's replicated state (<see cref="MatchState"/>): its own small ghost, because the Match object bakes
    /// the players, which must become ghosts of their own. Every map holds one; the scene wizard places it.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Match + "Match State")]
    [Icon(HyperRTSIcons.Match)]
    [HelpURL(HyperRTSDocs.Networking)]
    [DisallowMultipleComponent]
    [RequiresGhost]
    public class MatchStateAuthoring : AuthoringBehaviour
    {
        public class Baker : Baker<MatchStateAuthoring>
        {
            public override void Bake(MatchStateAuthoring authoring)
            {
                AddComponent(GetEntity(TransformUsageFlags.None), new MatchState { Phase = MatchPhase.Playing });
            }
        }
    }
}
