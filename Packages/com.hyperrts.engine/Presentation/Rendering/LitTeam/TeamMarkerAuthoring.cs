using HyperRTS.Simulation.Common.Authoring;
using UnityEngine;

namespace HyperRTS.Presentation.Rendering.LitTeam
{
    /// <summary>
    /// Marks a child mesh as a team marker (a roof panel, pennant, flag or placard), so validation counts markers
    /// without matching names. It bakes nothing: a LitTeam material with a white team mask takes the owner's colour.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Look + "Effects/Team Marker")]
    [Icon(HyperRTSIcons.TeamMarkerAuthoring)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshRenderer))]
    public class TeamMarkerAuthoring : AuthoringBehaviour
    {
    }
}
