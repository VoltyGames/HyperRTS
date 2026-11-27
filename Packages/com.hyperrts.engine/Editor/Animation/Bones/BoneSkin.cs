using System;
using System.Collections.Generic;
using HyperRTS.Editor.Common;
using HyperRTS.Presentation.Animation.Bones;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Animation.Bones;

/// <summary>Turns a model's nodes into meshes skinned to a <see cref="BoneRig"/>'s bones, with the skin in vertex channels.</summary>
internal sealed class BoneSkin
{
    private const float BindTolerance = 1e-3f;

    private readonly string _rigName;
    private readonly int _skeletonBones;
    private readonly Matrix4x4[] _binds;
    private readonly bool[] _upper;
    private readonly Func<string, int> _boneIndex;

    public BoneSkin(string rigName, int skeletonBones, Matrix4x4[] binds, bool[] upper, Func<string, int> boneIndex)
    {
        _rigName = rigName;
        _skeletonBones = skeletonBones;
        _binds = binds;
        _upper = upper;
        _boneIndex = boneIndex;
    }

    /// <summary>
    /// A skinned node's mesh moved into the skeleton mesh's rest space, with its skin in vertex channels.
    /// <paramref name="model"/> may be any model bound to the same skeleton at the same rest pose.
    /// </summary>
    public Mesh SkinnedMesh(GameObject model, string node)
    {
        var renderer = BoneRig.Find(model, node).GetComponent<SkinnedMeshRenderer>()
            .OrThrow($"Node '{node}' of '{model.name}' has no skinned mesh.");
        var bindposes = renderer.sharedMesh.bindposes;
        var remap = new int[renderer.bones.Length];
        for (var i = 0; i < remap.Length; i++)
        {
            remap[i] = _boneIndex(renderer.bones[i].name);
        }

        var into = _binds[remap[0]].inverse * bindposes[0];
        for (var i = 1; i < remap.Length; i++)
        {
            var same = Approximately(bindposes[i], _binds[remap[i]] * into);
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
    /// A copy of a rigid node's mesh, its origin set on the rig's <paramref name="socket"/> node and carried by that
    /// socket's own bone, in the skeleton mesh's rest space. The mesh must be in the rig's axes, as the socket node is.
    /// </summary>
    public Mesh AttachedMesh(GameObject model, string node, string socket)
    {
        var filter = BoneRig.Find(model, node).GetComponent<MeshFilter>();
        var source = (filter != null ? filter.sharedMesh : null).OrThrow($"Node '{node}' of '{model.name}' has no mesh.");
        var bone = _boneIndex(socket);
        if (bone < _skeletonBones)
        {
            throw new InvalidOperationException($"'{socket}' of '{_rigName}' is a skeleton bone, not a socket of the set.");
        }

        var mesh = Object.Instantiate(source);
        BoneMesh.Move(mesh, _binds[bone].inverse);
        var weights = new BoneWeight[mesh.vertexCount];
        Array.Fill(weights, new BoneWeight { boneIndex0 = 0, weight0 = 1f });
        Skin(mesh, weights, new[] { bone });
        return mesh;
    }

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
