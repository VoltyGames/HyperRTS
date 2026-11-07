using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Common.Authoring;
using UnityEngine;

namespace HyperRTS.Presentation.HUD.Map
{
    /// <summary>
    /// A top-down picture of the whole playable area that the minimap draws under its blips. Map tools bake it; one
    /// per scene.
    /// </summary>
    [AddComponentMenu("")]
    [Icon(HyperRTSIcons.MinimapImage)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public sealed class MinimapImage : PresentationBehaviour
    {
        [Tooltip("Covers the match's map rectangle exactly, north up.")]
        public Texture2D image;
    }
}
