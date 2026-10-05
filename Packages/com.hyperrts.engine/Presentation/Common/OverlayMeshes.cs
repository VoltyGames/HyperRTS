using UnityEngine;

namespace HyperRTS.Presentation.Common
{
    /// <summary>Procedural unit meshes for world overlays; scaled per instance by the draw matrix.</summary>
    public static class OverlayMeshes
    {
        /// <summary>1x1 quad on the XZ plane, centred, UV (0,0) at the min corner.</summary>
        public static Mesh FlatQuad() => Quad("Flat Quad", (x, y) => new Vector3(x, 0f, y));

        /// <summary>1x1 quad on the XY plane, centred; rotate by the camera rotation to face it.</summary>
        public static Mesh Billboard() => Quad("Billboard", (x, y) => new Vector3(x, y, 0f));

        /// <summary>Flat ring of outer radius 1 on the XZ plane.</summary>
        public static Mesh Ring(int segments, float innerRadius)
        {
            var vertices = new Vector3[segments * 2];
            var triangles = new int[segments * 6];
            for (var i = 0; i < segments; i++)
            {
                var angle = i * Mathf.PI * 2f / segments;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = direction * innerRadius;
                vertices[i * 2 + 1] = direction;

                var next = (i + 1) % segments;
                var t = i * 6;
                triangles[t] = i * 2;
                triangles[t + 1] = next * 2 + 1;
                triangles[t + 2] = i * 2 + 1;
                triangles[t + 3] = i * 2;
                triangles[t + 4] = next * 2;
                triangles[t + 5] = next * 2 + 1;
            }

            return Build("Ring", vertices, triangles);
        }

        /// <summary>Unit cube resting on the XZ plane (y from 0 to 1).</summary>
        public static Mesh Box()
        {
            var vertices = new Vector3[8];
            for (var i = 0; i < 8; i++)
            {
                vertices[i] = new Vector3((i & 1) - 0.5f, (i >> 1) & 1, ((i >> 2) & 1) - 0.5f);
            }

            int[] triangles =
            {
                0, 2, 1, 1, 2, 3, // -Z
                4, 5, 6, 5, 7, 6, // +Z
                0, 4, 2, 2, 4, 6, // -X
                1, 3, 5, 3, 7, 5, // +X
                2, 6, 3, 3, 6, 7, // +Y
                0, 1, 4, 1, 5, 4, // -Y
            };
            return Build("Box", vertices, triangles);
        }

        private static Mesh Quad(string name, System.Func<float, float, Vector3> place)
        {
            var vertices = new[] { place(-0.5f, -0.5f), place(0.5f, -0.5f), place(-0.5f, 0.5f), place(0.5f, 0.5f) };
            var mesh = Build(name, vertices, new[] { 0, 2, 1, 1, 2, 3 });
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            return mesh;
        }

        private static Mesh Build(string name, Vector3[] vertices, int[] triangles)
        {
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles, hideFlags = HideFlags.DontSave };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
