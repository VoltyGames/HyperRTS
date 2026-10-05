using HyperRTS.Core;
using UnityEngine;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>A kind of resource (supplies, gold, oil). Components reference it through <c>UnityObjectRef</c>.</summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Resources + "Resource Type", fileName = "ResourceType")]
    [HelpURL(HyperRTSDocs.Modules)]
    public class ResourceType : ScriptableObject
    {
        [Tooltip("Name shown in the HUD.")]
        public string displayName = "Supplies";

        [Tooltip("Icon shown next to amounts in the HUD.")]
        public Texture2D icon;

        [Tooltip("Accent colour for HUD text and minimap nodes.")]
        public Color color = new(1f, 0.8f, 0.25f);
    }
}
