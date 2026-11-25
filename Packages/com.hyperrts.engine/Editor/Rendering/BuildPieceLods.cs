using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Editor.Rendering
{
    /// <summary>
    /// Keeps a building's construction pieces whole in its Mesh LOD levels. Import and simplifying weld neighbouring pieces,
    /// and a triangle whose corners wait for different pieces would stretch from a placed piece to the origin of one not yet
    /// built, so each such triangle gets its own corners, waiting for the last of its pieces.
    /// </summary>
    public static class BuildPieceLods
    {
        // LitTeam's _BUILD reads each vertex's (order, is a piece) from UV3.
        private const VertexAttribute BuildOrder = VertexAttribute.TexCoord3;

        /// <summary>Gives every triangle that spans pieces its own corners; does nothing to a mesh without build orders or levels.</summary>
        public static void KeepPiecesWhole(Mesh mesh)
        {
            if (mesh.lodCount < 2 || !mesh.HasVertexAttribute(BuildOrder))
            {
                return;
            }

            RequireOneFloatStream(mesh);
            var orders = new List<Vector2>();
            mesh.GetUVs(3, orders);
            var stride = mesh.GetVertexBufferStride(0);
            var offset = mesh.GetVertexAttributeOffset(BuildOrder);
            var vertices = new List<byte>(ReadVertices(mesh));
            var indices = new List<int>[mesh.subMeshCount];
            var ranges = new MeshLodRange[mesh.subMeshCount][];
            for (var sub = 0; sub < mesh.subMeshCount; sub++)
            {
                indices[sub] = new List<int>();
                ranges[sub] = new MeshLodRange[mesh.lodCount];
                for (var lod = 0; lod < mesh.lodCount; lod++)
                {
                    var level = mesh.GetIndices(sub, lod, true);
                    ranges[sub][lod] = new MeshLodRange { indexStart = (uint)indices[sub].Count, indexCount = (uint)level.Length };
                    KeepTrianglesWhole(level, orders, vertices, stride, offset, indices[sub]);
                }
            }

            Write(mesh, vertices, indices, ranges);
        }

        private static void KeepTrianglesWhole(int[] level, List<Vector2> orders, List<byte> vertices, int stride, int offset, List<int> into)
        {
            for (var i = 0; i < level.Length; i += 3)
            {
                var corners = new[] { level[i], level[i + 1], level[i + 2] };
                if (!SpansPieces(orders, corners))
                {
                    into.AddRange(corners);
                    continue;
                }

                var last = Last(orders, corners);
                foreach (var corner in corners)
                {
                    into.Add(CopyCorner(vertices, stride, offset, corner, last));
                }
            }
        }

        private static void RequireOneFloatStream(Mesh mesh)
        {
            var oneStream = mesh.vertexBufferCount == 1;
            if (!oneStream || mesh.GetVertexAttributeFormat(BuildOrder) != VertexAttributeFormat.Float32 || mesh.GetVertexAttributeDimension(BuildOrder) < 2)
            {
                throw new InvalidOperationException($"{mesh.name}: build orders need one vertex stream with UV3 as two 32-bit floats.");
            }
        }

        private static byte[] ReadVertices(Mesh mesh)
        {
            using var read = Mesh.AcquireReadOnlyMeshData(mesh);
            return read[0].GetVertexData<byte>(0).ToArray();
        }

        private static bool SpansPieces(List<Vector2> orders, int[] corners)
        {
            var first = orders[corners[0]];
            foreach (var corner in corners)
            {
                if (orders[corner] != first)
                {
                    return true;
                }
            }

            return false;
        }

        // The order of the corner whose piece is placed last; a corner on no piece waits for nothing.
        private static Vector2 Last(List<Vector2> orders, int[] corners)
        {
            var last = new Vector2(0f, 0f);
            foreach (var corner in corners)
            {
                var order = orders[corner];
                if (order.y >= 0.5f && (last.y < 0.5f || order.x > last.x))
                {
                    last = order;
                }
            }

            return last;
        }

        private static int CopyCorner(List<byte> vertices, int stride, int offset, int corner, Vector2 order)
        {
            var copy = new byte[stride];
            vertices.CopyTo(corner * stride, copy, 0, stride);
            BitConverter.GetBytes(order.x).CopyTo(copy, offset);
            BitConverter.GetBytes(order.y).CopyTo(copy, offset + sizeof(float));
            vertices.AddRange(copy);
            return vertices.Count / stride - 1;
        }

        private static void Write(Mesh mesh, List<byte> vertices, List<int>[] indices, MeshLodRange[][] ranges)
        {
            var levels = mesh.lodCount;
            // Sub-meshes take their new indices as one level each, then split into levels again.
            mesh.lodCount = 1;
            using var data = new NativeArray<byte>(vertices.ToArray(), Allocator.Temp);
            MeshLods.Replace(mesh, data, indices, ranges, levels);
        }
    }
}
