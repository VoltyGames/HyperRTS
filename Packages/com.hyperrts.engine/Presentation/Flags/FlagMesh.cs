using HyperRTS.Simulation.Buildings;
using UnityEngine;

namespace HyperRTS.Presentation.Flags
{
    /// <summary>The cloth HyperRTS/Flag deforms: one flag height tall, hoist edge on x = 0, hanging down from the origin.</summary>
    public static class FlagMesh
    {
        /// <summary>Width over height of every flag; flag textures are resized to it.</summary>
        public const float Aspect = 1.5f;

        private const int Columns = 24;
        private const int Rows = 4;

        public static Mesh Build()
        {
            var vertices = new Vector3[(Columns + 1) * (Rows + 1)];
            var uvs = new Vector2[vertices.Length];
            var normals = new Vector3[vertices.Length];
            for (var row = 0; row <= Rows; row++)
            {
                for (var column = 0; column <= Columns; column++)
                {
                    var i = row * (Columns + 1) + column;
                    var uv = new Vector2((float)column / Columns, (float)row / Rows);
                    vertices[i] = new Vector3(uv.x * Aspect, uv.y - 1f, 0f);
                    uvs[i] = uv;
                    normals[i] = Vector3.back;
                }
            }

            var mesh = new Mesh { name = "Flag", vertices = vertices, uv = uvs, normals = normals, triangles = Triangles() };
            mesh.bounds = SwingBounds();
            return mesh;
        }

        private static int[] Triangles()
        {
            var triangles = new int[Columns * Rows * 6];
            var t = 0;
            for (var row = 0; row < Rows; row++)
            {
                for (var column = 0; column < Columns; column++)
                {
                    var i = row * (Columns + 1) + column;
                    var up = i + Columns + 1;
                    triangles[t++] = i;
                    triangles[t++] = up;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = up;
                    triangles[t++] = up + 1;
                }
            }

            return triangles;
        }

        // The shader turns the flag downwind and lowers it down the pole, so culling must see every pose.
        private static Bounds SwingBounds()
        {
            var bottom = -1f - FlagMount.MaxDrop;
            var bounds = new Bounds();
            bounds.SetMinMax(new Vector3(-Aspect, bottom, -Aspect), new Vector3(Aspect, 0.25f, Aspect));
            return bounds;
        }
    }
}
