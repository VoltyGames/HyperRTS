using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Editor.Rendering
{
    /// <summary>
    /// The one rule every Mesh LOD mesh switches by, a level per halving of screen size so models of any size and source
    /// drop detail alike, and packing hand-made levels into one mesh.
    /// </summary>
    public static class MeshLods
    {
        /// <summary>
        /// Pixels the bounds' diameter spans when level 1 takes over, at a Mesh LOD threshold of 1; level 2 takes over at
        /// half that, and so on. A quality level's threshold scales it.
        /// </summary>
        public const float FirstLevelPixels = 150f;

        /// <summary>
        /// Replaces the curve Unity fits from simplification error, which keeps a level until its error is under a pixel,
        /// with the size rule. Does nothing to a mesh with one level.
        /// </summary>
        public static void UseSizeRule(Mesh mesh)
        {
            if (mesh.lodCount < 2)
            {
                return;
            }

            // Entities Graphics picks level slope * log2(threshold / diameter pixels) + bias.
            mesh.lodSelectionCurve = new Mesh.LodSelectionCurve { lodSlope = 1f, lodBias = 1f + Mathf.Log(FirstLevelPixels, 2f) };
        }

        /// <summary>
        /// Packs hand-made levels, finest first, into the first mesh's Mesh LOD levels: their vertices appended and one
        /// index range per level in each sub-mesh. The levels need the same sub-meshes and one vertex stream alike.
        /// </summary>
        public static void Pack(Mesh[] meshes)
        {
            var target = meshes[0];
            var layout = target.GetVertexAttributes();
            foreach (var mesh in meshes)
            {
                RequireAlike(mesh, target, layout);
            }

            var vertexCount = meshes.Sum(mesh => mesh.vertexCount);
            var vertices = new NativeArray<byte>(vertexCount * target.GetVertexBufferStride(0), Allocator.Temp);
            var indices = new List<int>[target.subMeshCount];
            var ranges = new MeshLodRange[target.subMeshCount][];
            for (var sub = 0; sub < target.subMeshCount; sub++)
            {
                indices[sub] = new List<int>();
                ranges[sub] = new MeshLodRange[meshes.Length];
            }

            Append(meshes, vertices, indices, ranges);

            var bounds = target.bounds;
            target.SetVertexBufferParams(vertexCount, layout);
            target.SetVertexBufferData(vertices, 0, 0, vertices.Length, 0, MeshUpdateFlags.DontRecalculateBounds);
            vertices.Dispose();
            target.indexFormat = vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            for (var sub = 0; sub < target.subMeshCount; sub++)
            {
                target.SetIndices(indices[sub], MeshTopology.Triangles, sub, false);
            }

            target.lodCount = meshes.Length;
            for (var sub = 0; sub < target.subMeshCount; sub++)
            {
                target.SetLods(ranges[sub], sub, MeshUpdateFlags.DontRecalculateBounds);
            }

            target.bounds = bounds;
        }

        private static void RequireAlike(Mesh mesh, Mesh target, VertexAttributeDescriptor[] layout)
        {
            var alike = mesh.subMeshCount == target.subMeshCount && mesh.vertexBufferCount == 1;
            if (!alike || !mesh.GetVertexAttributes().SequenceEqual(layout))
            {
                throw new InvalidOperationException(
                    $"{mesh.name}: Mesh LOD levels need the sub-meshes and one vertex stream of '{target.name}'.");
            }
        }

        // Each level's vertices after the last, and its indices, shifted to match, as the level's range of each sub-mesh.
        private static void Append(Mesh[] meshes, NativeArray<byte> vertices, List<int>[] indices, MeshLodRange[][] ranges)
        {
            using var read = Mesh.AcquireReadOnlyMeshData(meshes);
            var stride = meshes[0].GetVertexBufferStride(0);
            var offset = 0;
            for (var level = 0; level < meshes.Length; level++)
            {
                var data = read[level];
                NativeArray<byte>.Copy(data.GetVertexData<byte>(0), 0, vertices, offset * stride, data.vertexCount * stride);
                for (var sub = 0; sub < indices.Length; sub++)
                {
                    var part = meshes[level].GetIndices(sub, true);
                    ranges[sub][level] = new MeshLodRange { indexStart = (uint)indices[sub].Count, indexCount = (uint)part.Length };
                    foreach (var index in part)
                    {
                        indices[sub].Add(index + offset);
                    }
                }

                offset += data.vertexCount;
            }
        }
    }
}
