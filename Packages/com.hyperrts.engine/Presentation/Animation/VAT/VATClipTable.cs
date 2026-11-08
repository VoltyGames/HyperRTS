using HyperRTS.Presentation.Animation.Clips;
using HyperRTS.Simulation.Common.Authoring;
using UnityEngine;

namespace HyperRTS.Presentation.Animation.VAT
{
    /// <summary>
    /// The width, clips and all-row bounds of one set of vertex animation textures. A rigid animation set's bake
    /// writes it.
    /// </summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Animation + "VAT Clip Table", fileName = "VATClipTable")]
    [HelpURL(HyperRTSDocs.Presentation)]
    public class VATClipTable : ClipTable
    {
        [Tooltip("Texture width: the column count the model's UV1 indexes.")]
        [Min(1)]
        public int width = 1;
    }
}
