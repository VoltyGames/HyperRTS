using UnityEngine;

namespace HyperRTS.Editor.Animation
{
    // The rest pose and hierarchy a live pose is rebuilt from.
    internal sealed partial class BoneRig
    {
        public bool HasBone(string bone) => _index.ContainsKey(bone);

        /// <summary>Per bone, in texture order, the bone above it in the rig: a socket's is its carrier, the root's -1.</summary>
        public int[] Parents()
        {
            var parents = new int[_upper.Length];
            for (var i = 0; i < _bones.Length; i++)
            {
                parents[i] = CarryingBone(_bones[i].parent) ?? -1;
            }

            for (var i = 0; i < _sockets.Count; i++)
            {
                parents[_bones.Length + i] = CarryingBone(_sockets[i].Node.parent) ?? -1;
            }

            return parents;
        }

        /// <summary>Per bone, from the skeleton mesh's rest space into the bone's: the matrix a skin matrix ends with.</summary>
        public Matrix4x4[] BindPoses()
        {
            var binds = new Matrix4x4[_upper.Length];
            for (var i = 0; i < _bones.Length; i++)
            {
                binds[i] = _bindposes[i];
            }

            for (var i = 0; i < _sockets.Count; i++)
            {
                binds[_bones.Length + i] = _sockets[i].Bind;
            }

            return binds;
        }
    }
}
