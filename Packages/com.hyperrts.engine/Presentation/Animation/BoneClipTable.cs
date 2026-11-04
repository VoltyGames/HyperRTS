using System.Collections.Generic;
using HyperRTS.Core;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>
    /// The bones, named clips and all-frame bounds of one bone animation texture, written by baking a
    /// <c>BoneAnimationSet</c>.
    /// </summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Animation + "Bone Clip Table", fileName = "BoneClipTable")]
    [HelpURL(HyperRTSDocs.Presentation)]
    public class BoneClipTable : ScriptableObject
    {
        [Tooltip("Bones in texture order: bone i's three texels start at column 3 i.")]
        public List<string> bones = new();

        [Tooltip("Texture rows, one per frame of every clip.")]
        [Min(1)]
        public int frames = 1;

        public List<VATClipTable.Clip> clips = new();

        [Tooltip("Object-space minimum over every frame of every clip.")]
        public Vector3 boundsMin;

        [Tooltip("Object-space maximum over every frame of every clip.")]
        public Vector3 boundsMax;

        public AABB Bounds => new MinMaxAABB { Min = boundsMin, Max = boundsMax };

        public int TextureWidth => bones.Count * 3;

        public VATClipRange Range(string clipName)
        {
            foreach (var clip in clips)
            {
                if (clip.name == clipName)
                {
                    return clip.Range;
                }
            }

            throw new KeyNotFoundException($"Bone clip table '{name}' has no clip '{clipName}'.");
        }
    }
}
