using HyperRTS.Core;
using UnityEngine;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>
    /// A top-down picture of the whole playable area that the minimap draws under its blips. Map tools bake it; one
    /// per scene.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.UI + "Minimap Image")]
    [Icon(HyperRTSIcons.UI)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    public sealed class MinimapImage : MonoBehaviour
    {
        [Tooltip("Covers the match's map rectangle exactly, north up.")]
        public Texture2D image;
    }
}
