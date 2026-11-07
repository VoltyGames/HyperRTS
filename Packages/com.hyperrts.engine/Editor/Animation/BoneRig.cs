using HyperRTS.Presentation.Animation.Bones;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Animation
{
    /// <summary>
    /// A temporary instance of a bone set's rig model, posed by its clips so the baked meshes need no skin and no
    /// object transform.
    /// </summary>
    internal sealed class BoneRig : IDisposable
    {
        private const float BindTolerance = 1e-3f;

        private readonly GameObject _root;
        private readonly Transform _rootBone;
        private readonly Dictionary<AnimationClip, (Vector3 Start, Vector3 End)> _travel = new();
        private readonly SkinnedMeshRenderer _body;
        private readonly Transform[] _bones;
        private readonly Matrix4x4[] _bindposes;
        private readonly bool[] _upper;
        private readonly Dictionary<string, int> _index = new();

        public BoneRig(GameObject model, string skeleton, string upperRoot, string rootBone)
        {
            _root = Object.Instantiate(model);
            _root.hideFlags = HideFlags.HideAndDontSave;
            _body = Find(_root, skeleton).GetComponent<SkinnedMeshRenderer>()
                ?? throw new InvalidOperationException($"Node '{skeleton}' of '{model.name}' has no skinned mesh to name the skeleton.");
            _rootBone = Find(_root, rootBone);
            _bones = _body.bones;
            _bindposes = _body.sharedMesh.bindposes;
            var top = Find(_root, upperRoot);
            _upper = new bool[_bones.Length];
            for (var i = 0; i < _bones.Length; i++)
            {
                _index.Add(_bones[i].name, i);
                _upper[i] = _bones[i].IsChildOf(top);
            }
        }

        public List<string> BoneNames()
        {
            return _bones.Select(bone => bone.name).ToList();
        }

        public int BoneIndex(string bone) => _index.TryGetValue(bone, out var found)
            ? found
            : throw new InvalidOperationException($"The skeleton of '{_root.name}' has no bone '{bone}'.");

        /// <summary>
        /// Every bone's skin matrix with <paramref name="clip"/> sampled at <paramref name="time"/>. In place removes the
        /// root bone's ground travel, the straight line from its first to its last position, and keeps its height and sway.
        /// </summary>
        public Matrix4x4[] Pose(AnimationClip clip, float time, bool inPlace)
        {
            // Measured before sampling: the first measure of a clip samples its ends, which would replace this pose.
            var drift = inPlace ? Drift(clip, time) : Vector3.zero;
            Sample(clip, time);
            _rootBone.position -= _root.transform.TransformVector(drift);

            var toRoot = _root.transform.worldToLocalMatrix;
            var pose = new Matrix4x4[_bones.Length];
            for (var i = 0; i < pose.Length; i++)
            {
                pose[i] = toRoot * _bones[i].localToWorldMatrix * _bindposes[i];
            }

            return pose;
        }

        /// <summary>Ground speed in m/s below which a clip counts as having none.</summary>
        public const float MinGroundSpeed = 0.1f;

        /// <summary>Metres per second the root bone travels over <paramref name="clip"/> played <paramref name="speed"/> times faster: the ground an in place pose removes. Under <see cref="MinGroundSpeed"/> is sway, not travel, and reads 0.</summary>
        public float GroundSpeed(AnimationClip clip, float speed)
        {
            var (start, end) = Travel(clip);
            var drift = _root.transform.TransformVector(end - start);
            drift.y = 0f;
            var ground = drift.magnitude * speed / clip.length;
            return ground < MinGroundSpeed ? 0f : ground;
        }

        private void Sample(AnimationClip clip, float time) => clip.SampleAnimation(_rootBone.parent.gameObject, time);

        /// <summary>The root's ground travel from the start of <paramref name="clip"/> to <paramref name="time"/> along its straight line, in the model root's space.</summary>
        private Vector3 Drift(AnimationClip clip, float time)
        {
            var (start, end) = Travel(clip);
            var drift = Vector3.Lerp(start, end, Mathf.Clamp01(time / clip.length)) - start;
            drift.y = 0f;
            return drift;
        }

        private (Vector3 Start, Vector3 End) Travel(AnimationClip clip)
        {
            if (!_travel.TryGetValue(clip, out var ends))
            {
                Sample(clip, 0f);
                var start = _root.transform.InverseTransformPoint(_rootBone.position);
                Sample(clip, clip.length);
                ends = (start, _root.transform.InverseTransformPoint(_rootBone.position));
                _travel.Add(clip, ends);
            }

            return ends;
        }

        /// <summary>
        /// A skinned node's mesh moved into the skeleton mesh's rest space, with its skin in vertex channels.
        /// <paramref name="model"/> may be any model bound to the same skeleton at the same rest pose.
        /// </summary>
        public Mesh SkinnedMesh(GameObject model, string node)
        {
            var renderer = Find(model, node).GetComponent<SkinnedMeshRenderer>()
                ?? throw new InvalidOperationException($"Node '{node}' of '{model.name}' has no skinned mesh.");
            var bindposes = renderer.sharedMesh.bindposes;
            var remap = new int[renderer.bones.Length];
            for (var i = 0; i < remap.Length; i++)
            {
                remap[i] = BoneIndex(renderer.bones[i].name);
            }

            var into = _bindposes[remap[0]].inverse * bindposes[0];
            for (var i = 1; i < remap.Length; i++)
            {
                var same = Approximately(bindposes[i], _bindposes[remap[i]] * into);
                if (!same)
                {
                    throw new InvalidOperationException($"Node '{node}' of '{model.name}' binds bone '{renderer.bones[i].name}' at another rest pose than the skeleton, so it can't share its bone texture.");
                }
            }

            var mesh = Object.Instantiate(renderer.sharedMesh);
            BoneMesh.Move(mesh, into);
            Skin(mesh, renderer.sharedMesh.boneWeights, remap);
            return mesh;
        }

        /// <summary>
        /// A copy of a rigid node's mesh, its origin set on the rig's <paramref name="socket"/> node and carried by the
        /// skeleton bone at or above it, in the skeleton mesh's rest space.
        /// </summary>
        public Mesh AttachedMesh(GameObject model, string node, string socket)
        {
            var source = Find(model, node).GetComponent<MeshFilter>()?.sharedMesh
                ?? throw new InvalidOperationException($"Node '{node}' of '{model.name}' has no mesh.");
            var mesh = Object.Instantiate(source);
            var seat = Find(_root, socket);
            var weighted = CarryingBone(seat)
                ?? throw new InvalidOperationException($"Socket node '{socket}' of '{_root.name}' hangs from no skeleton bone.");
            // Onto the socket, into the bone's own space, then back through its bind pose into the skeleton mesh's rest space.
            BoneMesh.Move(mesh, _bindposes[weighted].inverse * _bones[weighted].worldToLocalMatrix * seat.localToWorldMatrix);
            var weights = new BoneWeight[mesh.vertexCount];
            Array.Fill(weights, new BoneWeight { boneIndex0 = 0, weight0 = 1f });
            Skin(mesh, weights, new[] { weighted });
            return mesh;
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

        // Indices into the skeleton in the index channel, weights in the weight channel, negative for upper-body bones.
        private void Skin(Mesh mesh, BoneWeight[] weights, int[] remap)
        {
            if (weights.Length != mesh.vertexCount)
            {
                throw new InvalidOperationException($"Mesh '{mesh.name}' has no bone weights to move into vertex channels.");
            }

            var indices = new List<Vector4>(weights.Length);
            var strengths = new List<Vector4>(weights.Length);
            foreach (var w in weights)
            {
                indices.Add(new Vector4(remap[w.boneIndex0], remap[w.boneIndex1], remap[w.boneIndex2], remap[w.boneIndex3]));
                strengths.Add(new Vector4(
                    Signed(w.weight0, remap[w.boneIndex0]), Signed(w.weight1, remap[w.boneIndex1]),
                    Signed(w.weight2, remap[w.boneIndex2]), Signed(w.weight3, remap[w.boneIndex3])));
            }

            mesh.SetUVs(BoneAnimatedAuthoring.IndexChannel, indices);
            mesh.SetUVs(BoneAnimatedAuthoring.WeightChannel, strengths);
            mesh.boneWeights = Array.Empty<BoneWeight>();
            mesh.bindposes = Array.Empty<Matrix4x4>();
        }

        private float Signed(float weight, int bone) => _upper[bone] ? -weight : weight;

        private static Transform Find(GameObject model, string node)
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

        private static bool Approximately(Matrix4x4 a, Matrix4x4 b)
        {
            for (var i = 0; i < 16; i++)
            {
                if (Mathf.Abs(a[i] - b[i]) > BindTolerance)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
