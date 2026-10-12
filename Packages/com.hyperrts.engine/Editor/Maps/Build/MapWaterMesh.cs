using System.Collections.Generic;
using HyperRTS.Editor.Maps.Geometry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Build
{
    /// <summary>
    /// River surfaces as ribbons at the water level, wide enough to tuck under the banks. UV is in metres: U across
    /// the river, V downstream, so a water shader can scroll with the current.
    /// </summary>
    public static class MapWaterMesh
    {
        private const float Step = 2f;
        private const float BankMargin = 6f;
        private const float EdgeOverhang = 12f;

        /// <summary>River ribbons at <paramref name="height"/>, run on past the terrain's edge (<paramref name="bounds"/>).</summary>
        public static Mesh Build(MapShapes shapes, float height, Rect bounds)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            foreach (var river in shapes.Rivers)
            {
                var half = river.Spec.width * 0.5f * (1f + river.Spec.widthVariation) + BankMargin;
                AddRibbon(MapGeometry.ExtendAtEdge(river.Line.Points, bounds, EdgeOverhang), half, height, vertices, uvs, triangles);
            }

            var mesh = new Mesh { name = "Rivers", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddRibbon(List<Vector2> line, float half, float height, List<Vector3> vertices,
            List<Vector2> uvs, List<int> triangles)
        {
            var length = MapGeometry.Length(line);
            var count = Mathf.Max(2, Mathf.CeilToInt(length / Step) + 1);
            var walker = new MapPathWalker(line);
            var first = vertices.Count;
            for (var i = 0; i < count; i++)
            {
                var along = Mathf.Min(length, i * Step);
                var point = walker.PointAt(along, out _);
                var ahead = walker.PointAt(Mathf.Min(length, along + Step), out _);
                var behind = walker.PointAt(Mathf.Max(0f, along - Step), out _);
                var direction = (ahead - behind).normalized;
                var left = new Vector2(-direction.y, direction.x) * half;
                vertices.Add(new Vector3(point.x + left.x, height, point.y + left.y));
                vertices.Add(new Vector3(point.x - left.x, height, point.y - left.y));
                uvs.Add(new Vector2(-half, along));
                uvs.Add(new Vector2(half, along));
                if (i == 0)
                {
                    continue;
                }

                var a = first + (i - 1) * 2;
                triangles.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
            }
        }
    }
}
