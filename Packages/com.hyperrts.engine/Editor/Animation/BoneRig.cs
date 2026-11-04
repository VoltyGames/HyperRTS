using System;
using System.Collections.Generic;
using HyperRTS.Presentation.Animation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Animation
{
    /// <summary>
    /// A temporary instance of a bone set's rig model, posed by its clips. Its first skinned part names the skeleton;
    /// every bone's skin matrix maps that mesh's rest vertices to where the clip's frame puts them, in the model root's
    /// space, so the baked meshes need no skin and no object transform.
    /// </summary>
    internal sealed class BoneRig : IDisposable
    {
        private const float BindTolerance = 1e-3f;

        private readonly GameObject _root;
        private readonly SkinnedMeshRenderer _body;
        private readonly Transform[] _bones;
        private readonly Matrix4x4[] _bindposes;
        private readonly bool[] _upper;
        private readonly Dictionary<string, int> _index = new();

        public BoneRig(GameObject model, string bodyNode, string upperRoot)
        {
            _root = Object.Instantiate(model);
            _root.hideFlags = HideFlags.HideAndDontSave;
            _body = Find(bodyNode).GetComponent<SkinnedMeshRenderer>()
                ?? throw new InvalidOperationException($"Node '{bodyNode}' of '{model.name}' has no skinned mesh to name the skeleton.");
            _bones = _body.bones;
            _bindposes = _body.sharedMesh.bindposes;
            var top = Find(upperRoot);
            _upper = new bool[_bones.Length];
            for (var i = 0; i < _bones.Length; i++)
            {
                _index.Add(_bones[i].name, i);
                _upper[i] = _bones[i].IsChildOf(top);
            }
        }

        public List<string> BoneNames()
        {
            var names = new List<string>(_bones.Length);
            foreach (var bone in _bones)
            {
                names.Add(bone.name);
            }

            return names;
        }

        public int BoneIndex(string bone) => _index.TryGetValue(bone, out var found)
            ? found
            : throw new InvalidOperationException($"The skeleton of '{_root.name}' has no bone '{bone}'.");

        /// <summary>Every bone's skin matrix with <paramref name="clip"/> sampled at <paramref name="time"/>.</summary>
        public Matrix4x4[] Pose(AnimationClip clip, float time)
        {
            clip.SampleAnimation(_root, time);
            var toRoot = _root.transform.worldToLocalMatrix;
            var pose = new Matrix4x4[_bones.Length];
            for (var i = 0; i < pose.Length; i++)
            {
                pose[i] = toRoot * _bones[i].localToWorldMatrix * _bindposes[i];
            }

            return pose;
        }

        /// <summary>A copy of a skinned node's mesh with its skin moved into vertex channels.</summary>
        public Mesh SkinnedMesh(string node)
        {
            var renderer = Find(node).GetComponent<SkinnedMeshRenderer>()
                ?? throw new InvalidOperationException($"Node '{node}' of '{_root.name}' has no skinned mesh.");
            var remap = new int[renderer.bones.Length];
            for (var i = 0; i < remap.Length; i++)
            {
                remap[i] = BoneIndex(renderer.bones[i].name);
                var same = Approximately(renderer.sharedMesh.bindposes[i], _bindposes[remap[i]]);
                if (!same)
                {
                    throw new InvalidOperationException($"Node '{node}' binds bone '{renderer.bones[i].name}' differently from the skeleton, so it can't share its bone texture.");
                }
            }

            var mesh = Object.Instantiate(renderer.sharedMesh);
            Skin(mesh, renderer.sharedMesh.boneWeights, remap);
            return mesh;
        }

        /// <summary>A copy of a rigid node's mesh carried by <paramref name="bone"/>, in the skeleton mesh's rest space.</summary>
        public Mesh AttachedMesh(string node, string bone)
        {
            var holder = Find(node);
            var source = holder.GetComponent<MeshFilter>()?.sharedMesh
                ?? throw new InvalidOperationException($"Node '{node}' of '{_root.name}' has no mesh.");
            var mesh = Object.Instantiate(source);
            var weighted = BoneIndex(bone);
            // Into the bone's own space, then back through its bind pose into the skeleton mesh's rest space.
            var into = _bindposes[weighted].inverse * _bones[weighted].worldToLocalMatrix * holder.localToWorldMatrix;
            var normals = into.inverse.transpose;
            var vertices = mesh.vertices;
            var directions = mesh.normals;
            for (var i = 0; i < vertices.Length; i++)
            {
                vertices[i] = into.MultiplyPoint3x4(vertices[i]);
                directions[i] = normals.MultiplyVector(directions[i]).normalized;
            }

            mesh.vertices = vertices;
            mesh.normals = directions;
            var weights = new BoneWeight[vertices.Length];
            Array.Fill(weights, new BoneWeight { boneIndex0 = 0, weight0 = 1f });
            Skin(mesh, weights, new[] { weighted });
            return mesh;
        }

        public void Dispose() => Object.DestroyImmediate(_root);

        // Indices into the skeleton in channel 4, weights in channel 5, negative for upper-body bones.
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

        private Transform Find(string node)
        {
            Transform found = null;
            foreach (var candidate in _root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name != node)
                {
                    continue;
                }

                if (found != null)
                {
                    throw new InvalidOperationException($"'{_root.name}' has more than one node named '{node}'.");
                }

                found = candidate;
            }

            return found != null ? found : throw new InvalidOperationException($"'{_root.name}' has no node named '{node}'.");
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
