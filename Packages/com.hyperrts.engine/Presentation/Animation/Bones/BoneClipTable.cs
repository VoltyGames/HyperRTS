using System.Collections.Generic;
using HyperRTS.Presentation.Animation.Clips;
using HyperRTS.Simulation.Common.Authoring;
using UnityEngine;

namespace HyperRTS.Presentation.Animation.Bones
{
    /// <summary>
    /// The bones, named clips and all-row bounds of one bone animation texture, written by baking a
    /// <c>BoneAnimationSet</c>.
    /// </summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Animation + "Bone Clip Table", fileName = "BoneClipTable")]
    [HelpURL(HyperRTSDocs.Presentation)]
    public class BoneClipTable : ClipTable
    {
        [Tooltip("Bones in texture order: bone i's three texels start at column 3 i.")]
        public List<string> bones = new();

        [Tooltip("Texture rows, one per frame of every clip.")]
        [Min(1)]
        public int frames = 1;

        public int TextureWidth => bones.Count * 3;
    }
}
