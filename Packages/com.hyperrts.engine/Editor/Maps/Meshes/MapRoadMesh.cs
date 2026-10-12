using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Ground;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Editor.Maps.Meshes
{
    /// <summary>
    /// One road class's ribbons laid over the terrain, turned into one mesh: rows every couple of metres, vertices
    /// across each road at ±width/2 and between, lifted a few centimetres over the final ground and lit with the
    /// ground's normal. UV X runs across (0 to 1), UV Y is metres along the road, and vertex alpha fades the ends the
    /// road should fade at; the shader reads no other colour.
    /// </summary>
    public sealed class MapRoadMesh
    {
        public const float Step = 2f;
        public const float Lift = 0.05f;
        public const float FadeLength = 4f;

        private const float AcrossStep = 1.5f;
        private const float NormalSpan = 1f;

        public List<Vector3> Vertices { get; } = new();

        public List<Vector3> Normals { get; } = new();

        public List<Vector4> Tangents { get; } = new();

        public List<Vector2> Uvs { get; } = new();

        public List<Color32> Colors { get; } = new();

        public List<int> Triangles { get; } = new();

        public int Count => Vertices.Count;

        /// <summary>
        /// Adds a road's ribbon. <paramref name="groundLevel"/> is the terrain height of world y = 0; rows whose centre
        /// is <paramref name="hidden"/> (over water, under a bridge) are left out and break the ribbon there.
        /// </summary>
        public void AddRibbon(MapRoad road, bool fadeStart, bool fadeEnd, MapHeights heights, float groundLevel,
            Func<Vector2, bool> hidden)
        {
            var points = MapCurves.Resample(road.Line.Points, Step);
            var length = MapGeometry.Length(points);
            var columns = Mathf.Max(3, Mathf.CeilToInt(road.Width / AcrossStep) + 1);
            var previous = -1;
            var along = 0f;
            for (var i = 0; i < points.Count; i++)
            {
                along += i > 0 ? (points[i] - points[i - 1]).magnitude : 0f;
                if (hidden(points[i]))
                {
                    previous = -1;
                    continue;
                }

                var ahead = points[Mathf.Min(i + 1, points.Count - 1)] - points[Mathf.Max(i - 1, 0)];
                var alpha = Mathf.Min(fadeStart ? along / FadeLength : 1f, fadeEnd ? (length - along) / FadeLength : 1f);
                var row = Count;
                AddRow(points[i], ahead.normalized, road.Width, columns, along, Mathf.Clamp01(alpha), heights, groundLevel);
                if (previous >= 0)
                {
                    AddQuads(previous, row, columns);
                }

                previous = row;
            }
        }

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

        private void AddRow(Vector2 center, Vector2 forward, float width, int columns, float along, float alpha,
            MapHeights heights, float groundLevel)
        {
            var right = MapGeometry.Right(forward);
            var fade = (byte)Mathf.RoundToInt(alpha * 255f);
            for (var c = 0; c < columns; c++)
            {
                var across = (float)c / (columns - 1);
                var point = center + right * ((across - 0.5f) * width);
                var normal = Normal(heights, point);
                var side = new Vector3(right.x, 0f, right.y);
                var tangent = (side - normal * Vector3.Dot(side, normal)).normalized;
                Vertices.Add(new Vector3(point.x, heights.Height(point) - groundLevel + Lift, point.y));
                Normals.Add(normal);
                Tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, -1f));
                Uvs.Add(new Vector2(across, along));
                Colors.Add(new Color32(255, 255, 255, fade));
            }
        }

        /// <summary>Two triangles per cell, clockwise seen from above.</summary>
        private void AddQuads(int previous, int row, int columns)
        {
            for (var c = 0; c + 1 < columns; c++)
            {
                var a = previous + c;
                var b = row + c;
                Triangles.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
            }
        }

        private static Vector3 Normal(MapHeights heights, Vector2 point)
        {
            var dx = heights.Height(point + Vector2.right * NormalSpan) - heights.Height(point - Vector2.right * NormalSpan);
            var dz = heights.Height(point + Vector2.up * NormalSpan) - heights.Height(point - Vector2.up * NormalSpan);
            return new Vector3(-dx, 2f * NormalSpan, -dz).normalized;
        }
    }
}
