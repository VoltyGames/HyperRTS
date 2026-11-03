using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment.Vegetation.RowCrops
{
    /// <summary>
    /// One block of a crop's canopy as vertex lists, laid on the ground: HyperRTS/RowCropCanopy raises each vertex by
    /// its rise times the crop's height there.
    /// </summary>
    public sealed class RowCropCanopyGeometry
    {
        /// <summary>World positions on the ground; wall bottoms lie under it.</summary>
        public readonly List<Vector3> Positions = new();

        public readonly List<Vector3> Normals = new();

        /// <summary>Per vertex: x wall tiles along the outline, y rise (1 at the top, 0 at a wall's foot), z 1 on walls, w field id.</summary>
        public readonly List<Vector4> Canopy = new();

        public readonly List<int> Triangles = new();

        public int Add(Vector3 position, Vector3 normal, Vector4 canopy)
        {
            Positions.Add(position);
            Normals.Add(normal);
            Canopy.Add(canopy);
            return Positions.Count - 1;
        }

        public void AddTriangle(int a, int b, int c)
        {
            Triangles.Add(a);
            Triangles.Add(b);
            Triangles.Add(c);
        }

        /// <summary>Whether a vertex belongs to a wall rather than the top.</summary>
        public bool OnWall(int vertex) => Canopy[vertex].z > 0.5f;

        /// <summary>The mesh, its bounds grown up by <paramref name="rise"/> metres, the most the shader raises it.</summary>
        public Mesh CreateMesh(string name, float rise)
        {
            var mesh = new Mesh
            {
                name = name,
                hideFlags = HideFlags.DontSave,
                indexFormat = Positions.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16,
            };
            mesh.SetVertices(Positions);
            mesh.SetNormals(Normals);
            mesh.SetUVs(0, Canopy);
            mesh.SetTriangles(Triangles, 0, false);
            mesh.RecalculateBounds();
            var bounds = mesh.bounds;
            bounds.max += Vector3.up * rise;
            mesh.bounds = bounds;
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
