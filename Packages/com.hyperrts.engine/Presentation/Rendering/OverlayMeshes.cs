using UnityEngine;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>Procedural unit meshes for world overlays; scaled per instance by the draw matrix.</summary>
    public static class OverlayMeshes
    {
        /// <summary>1x1 quad on the XZ plane, centred, UV (0,0) at the min corner.</summary>
        public static Mesh FlatQuad() => Quad("Flat Quad", (x, y) => new Vector3(x, 0f, y));

        /// <summary>1x1 quad on the XY plane, centred; rotate by the camera rotation to face it.</summary>
        public static Mesh Billboard() => Quad("Billboard", (x, y) => new Vector3(x, y, 0f));

        /// <summary>1x1 grid of <paramref name="cells"/> squares a side on the XZ plane, centred, for draping on terrain.</summary>
        public static Mesh Grid(int cells)
        {
            var side = cells + 1;
            var vertices = new Vector3[side * side];
            for (var i = 0; i < vertices.Length; i++)
            {
                vertices[i] = new Vector3((float)(i % side) / cells - 0.5f, 0f, (float)(i / side) / cells - 0.5f);
            }

            var triangles = new int[cells * cells * 6];
            for (var cell = 0; cell < cells * cells; cell++)
            {
                var corner = cell / cells * side + cell % cells;
                var t = cell * 6;
                triangles[t] = corner;
                triangles[t + 1] = corner + side;
                triangles[t + 2] = corner + 1;
                triangles[t + 3] = corner + 1;
                triangles[t + 4] = corner + side;
                triangles[t + 5] = corner + side + 1;
            }

            return Build("Grid", vertices, triangles);
        }

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

        /// <summary>
        /// One of <paramref name="segments"/> pieces of a ring of outer radius 1 on the XY plane, from 12 o'clock
        /// clockwise; dials draw a piece per filled step, turned about Z.
        /// </summary>
        public static Mesh DialSegment(int segments, float innerRadius)
        {
            var angle = Mathf.PI * 2f / segments;
            var start = new Vector3(0f, 1f, 0f);
            var end = new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f);
            var vertices = new[] { start * innerRadius, start, end * innerRadius, end };
            return Build("Dial Segment", vertices, new[] { 0, 1, 3, 0, 3, 2 });
        }

        /// <summary>Disc of radius 1 on the XY plane; rotate by the camera rotation to face it.</summary>
        public static Mesh Disc(int segments)
        {
            var vertices = new Vector3[segments + 1];
            var triangles = new int[segments * 3];
            for (var i = 0; i < segments; i++)
            {
                var angle = i * Mathf.PI * 2f / segments;
                vertices[i + 1] = new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f);
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % segments + 1;
            }

            return Build("Disc", vertices, triangles);
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
