using System.Collections.Generic;
using HyperRTS.Presentation.Animation;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Animation
{
    /// <summary>Mesh edits for the bone bake that need no rig: joining parts into one mesh and moving a mesh rigidly.</summary>
    internal static class BoneMesh
    {
        /// <summary>
        /// One mesh of <paramref name="meshes"/> in order, as one submesh, bone channels kept; the inputs are destroyed.
        /// A single mesh comes back as it is.
        /// </summary>
        public static Mesh Merge(List<Mesh> meshes)
        {
            if (meshes.Count == 1)
            {
                return meshes[0];
            }

            var indices = new List<Vector4>();
            var weights = new List<Vector4>();
            var combine = new CombineInstance[meshes.Count];
            var vertices = 0;
            for (var i = 0; i < meshes.Count; i++)
            {
                var some = new List<Vector4>();
                meshes[i].GetUVs(BoneAnimatedAuthoring.IndexChannel, some);
                indices.AddRange(some);
                meshes[i].GetUVs(BoneAnimatedAuthoring.WeightChannel, some);
                weights.AddRange(some);
                combine[i] = new CombineInstance { mesh = meshes[i], transform = Matrix4x4.identity };
                vertices += meshes[i].vertexCount;
            }

            var merged = new Mesh { indexFormat = vertices > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            merged.CombineMeshes(combine, mergeSubMeshes: true, useMatrices: false);
            merged.SetUVs(BoneAnimatedAuthoring.IndexChannel, indices);
            merged.SetUVs(BoneAnimatedAuthoring.WeightChannel, weights);
            foreach (var mesh in meshes)
            {
                Object.DestroyImmediate(mesh);
            }

            return merged;
        }

        // Moves vertices, normals and tangents by a rigid transform, keeping each tangent's handedness.
        public static void Move(Mesh mesh, Matrix4x4 by)
        {
            var normals = by.inverse.transpose;
            var vertices = mesh.vertices;
            var directions = mesh.normals;
            var tangents = mesh.tangents;
            for (var i = 0; i < vertices.Length; i++)
            {
                vertices[i] = by.MultiplyPoint3x4(vertices[i]);
            }

            for (var i = 0; i < directions.Length; i++)
            {
                directions[i] = normals.MultiplyVector(directions[i]).normalized;
            }

            for (var i = 0; i < tangents.Length; i++)
            {
                var turned = by.MultiplyVector(tangents[i]).normalized;
                tangents[i] = new Vector4(turned.x, turned.y, turned.z, tangents[i].w);
            }

            mesh.vertices = vertices;
            mesh.normals = directions;
            mesh.tangents = tangents;
            mesh.RecalculateBounds();
        }
    }
}
