using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Animation.Bones
{
    /// <summary>
    /// A temporary instance of a bone set's rig model, posed by its clips so the baked meshes need no skin and no
    /// object transform. Its bones are the skeleton's, root first, then one per socket.
    /// </summary>
    internal sealed class BoneRig : IDisposable
    {
        private readonly GameObject _root;
        private readonly RootTravel _travel;
        private readonly SkinnedMeshRenderer _body;
        private readonly Transform[] _bones;
        private readonly Matrix4x4[] _bindposes;
        private readonly bool[] _upper;
        private readonly Dictionary<string, int> _index = new();
        private readonly List<SocketBone> _sockets = new();

        public BoneRig(GameObject model, string skeleton, string upperRoot, string rootBone,
            IEnumerable<BoneAnimationSet.Socket> sockets)
        {
            _root = Object.Instantiate(model);
            _root.hideFlags = HideFlags.HideAndDontSave;
            _body = Find(_root, skeleton).GetComponent<SkinnedMeshRenderer>()
                .OrThrow($"Node '{skeleton}' of '{model.name}' has no skinned mesh to name the skeleton.");
            var rootNode = Find(_root, rootBone);
            _travel = new RootTravel(_root, rootNode);
            _bones = _body.bones;
            _bindposes = _body.sharedMesh.bindposes;
            if (_bones[0] != rootNode)
            {
                throw new InvalidOperationException($"The skeleton of '{model.name}' starts with '{_bones[0].name}', not its root bone '{rootBone}', which the shader sets the upper body on.");
            }

            for (var i = 0; i < _bones.Length; i++)
            {
                _index.Add(_bones[i].name, i);
            }

            foreach (var socket in sockets)
            {
                var bone = CreateSocketBone(socket);
                _index.Add(socket.node, _bones.Length + _sockets.Count);
                _sockets.Add(bone);
            }

            _upper = UpperFlags(Find(_root, upperRoot));
            Meshes = new BoneSkin(_root.name, _bones.Length, BindPoses(), _upper, BoneIndex);
        }

        /// <summary>Turns the models' nodes into meshes skinned to this rig's bones.</summary>
        public BoneSkin Meshes { get; }

        private bool[] UpperFlags(Transform top)
        {
            var upper = new bool[_bones.Length + _sockets.Count];
            for (var i = 0; i < _bones.Length; i++)
            {
                upper[i] = _bones[i].IsChildOf(top);
            }

            for (var i = 0; i < _sockets.Count; i++)
            {
                upper[_bones.Length + i] = upper[_sockets[i].Carrier];
            }

            return upper;
        }

        public List<string> BoneNames() =>
            _bones.Select(bone => bone.name).Concat(_sockets.Select(socket => socket.Node.name)).ToList();

        /// <summary>Which bones, in texture order, belong to the upper body.</summary>
        public bool[] UpperBones() => (bool[])_upper.Clone();

        public int BoneIndex(string bone) => _index.TryGetValue(bone, out var found)
            ? found
            : throw new InvalidOperationException($"The skeleton of '{_root.name}' has no bone '{bone}'.");

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

        /// <summary>
        /// Every bone's skin matrix with <paramref name="clip"/> sampled at <paramref name="time"/>. In place drops the
        /// root bone's straight ground travel; both hands swing supported gear to meet the other hand.
        /// </summary>
        public Matrix4x4[] Pose(AnimationClip clip, float time, bool inPlace, bool bothHands)
        {
            _travel.Sample(clip, time, inPlace);

            var toRoot = _root.transform.worldToLocalMatrix;
            var pose = new Matrix4x4[_upper.Length];
            for (var i = 0; i < _bones.Length; i++)
            {
                pose[i] = toRoot * _bones[i].localToWorldMatrix * _bindposes[i];
            }

            for (var i = 0; i < _sockets.Count; i++)
            {
                pose[_bones.Length + i] = toRoot * _sockets[i].Placed(bothHands) * _sockets[i].Bind;
            }

            return pose;
        }

        /// <summary>Metres per second the root bone travels over <paramref name="clip"/> played <paramref name="speed"/> times faster: the ground an in place pose removes.</summary>
        public float GroundSpeed(AnimationClip clip, float speed) => _travel.GroundSpeed(clip, speed);

        private SocketBone CreateSocketBone(BoneAnimationSet.Socket socket)
        {
            var node = Find(_root, socket.node);
            var carrier = CarryingBone(node)
                ?? throw new InvalidOperationException($"Socket node '{socket.node}' of '{_root.name}' hangs from no skeleton bone.");
            Transform support = null;
            Transform hand = null;
            if (!string.IsNullOrEmpty(socket.support))
            {
                support = Find(_root, socket.support);
                if (!support.IsChildOf(node))
                {
                    throw new InvalidOperationException($"Support '{socket.support}' of socket '{socket.name}' is not under its node '{socket.node}'.");
                }

                hand = _bones[BoneIndex(socket.supportHand)];
            }

            // Like a bind pose: from the skeleton mesh's rest space into the socket node's space.
            var bind = node.worldToLocalMatrix * _bones[carrier].localToWorldMatrix * _bindposes[carrier];
            return new SocketBone(node, carrier, support, hand, bind);
        }

        // Index of the skeleton bone at or above the node, if any.
        private int? CarryingBone(Transform node)
        {
            for (var at = node; at != null; at = at.parent)
            {
                if (_index.TryGetValue(at.name, out var found))
                {
                    return found;
                }
            }

            return null;
        }

        public void Dispose() => Object.DestroyImmediate(_root);

        internal static Transform Find(GameObject model, string node)
        {
            Transform found = null;
            foreach (var candidate in model.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name != node)
                {
                    continue;
                }

                if (found != null)
                {
                    throw new InvalidOperationException($"'{model.name}' has more than one node named '{node}'.");
                }

                found = candidate;
            }

            return found != null ? found : throw new InvalidOperationException($"'{model.name}' has no node named '{node}'.");
        }
    }
}
