using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Editor.Maps.Meshes
{
    /// <summary>Vertex streams of road ribbons being built, turned into one mesh per road class.</summary>
    public sealed class MapRoadMesh
    {
        public List<Vector3> Vertices { get; } = new();

        public List<Vector3> Normals { get; } = new();

        public List<Vector4> Tangents { get; } = new();

        /// <summary>X across the road from 0 (left) to 1 (right), Y metres along it from its start.</summary>
        public List<Vector2> Uvs { get; } = new();

        /// <summary>Alpha is the fade at free ends and junctions; the shader reads nothing else.</summary>
        public List<Color32> Colors { get; } = new();

        public List<int> Triangles { get; } = new();

        public int Count => Vertices.Count;

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(Vertices);
            mesh.SetNormals(Normals);
            mesh.SetTangents(Tangents);
            mesh.SetUVs(0, Uvs);
            mesh.SetColors(Colors);
            mesh.SetTriangles(Triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
