using HyperRTS.Presentation.Animation.Bones;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Editor.Animation
{
    /// <summary>The skin matrices of every bone for every baked frame: the bone texture's pixels and the all-frame bounds.</summary>
    internal sealed class BoneFrames
    {
        private const int TexelsPerBone = 3;

        private readonly List<Matrix4x4[]> _frames = new();

        /// <summary>Poses the rig at every frame of every plan, in order.</summary>
        public static BoneFrames Sample(BoneRig rig, List<ClipFrames> plans)
        {
            var frames = new BoneFrames();
            foreach (var plan in plans)
            {
                for (var i = 0; i < plan.Count; i++)
                {
                    frames._frames.Add(rig.Pose(plan.Clip.source, plan.TimeOf(i), plan.Clip.inPlace));
                }
            }

            return frames;
        }

        /// <summary>Half-float RGBA, a row per frame and three texels a bone holding the matrix's top three rows.</summary>
        public Texture2D Texture(int bones)
        {
            var texture = new Texture2D(bones * TexelsPerBone, _frames.Count, TextureFormat.RGBAHalf, false, true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[bones * TexelsPerBone * _frames.Count];
            for (var frame = 0; frame < _frames.Count; frame++)
            {
                for (var bone = 0; bone < bones; bone++)
                {
                    var m = _frames[frame][bone];
                    for (var k = 0; k < TexelsPerBone; k++)
                    {
                        pixels[(frame * bones + bone) * TexelsPerBone + k] = new Color(m[k, 0], m[k, 1], m[k, 2], m[k, 3]);
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>Where the meshes reach over every frame, skinned as the shader does.</summary>
        public Bounds Bounds(IEnumerable<Mesh> meshes)
        {
            var bounds = new Bounds();
            var first = true;
            var indices = new List<Vector4>();
            var weights = new List<Vector4>();
            foreach (var mesh in meshes)
            {
                var vertices = mesh.vertices;
                mesh.GetUVs(BoneAnimatedAuthoring.IndexChannel, indices);
                mesh.GetUVs(BoneAnimatedAuthoring.WeightChannel, weights);
                foreach (var pose in _frames)
                {
                    for (var v = 0; v < vertices.Length; v++)
                    {
                        var point = Skinned(pose, vertices[v], indices[v], weights[v]);
                        if (first)
                        {
                            bounds = new Bounds(point, Vector3.zero);
                            first = false;
                        }
                        else
                        {
                            bounds.Encapsulate(point);
                        }
                    }
                }
            }

            return bounds;
        }

        private static Vector3 Skinned(Matrix4x4[] pose, Vector3 vertex, Vector4 indices, Vector4 weights)
        {
            var point = Vector3.zero;
            for (var i = 0; i < 4; i++)
            {
                var weight = Mathf.Abs(weights[i]);
                if (weight > 0f)
                {
                    point += weight * pose[(int)indices[i]].MultiplyPoint3x4(vertex);
                }
            }

            return point;
        }
    }
}
