using System;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Ground;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Meshes
{
    /// <summary>
    /// A road as a ribbon laid over the terrain: rows every couple of metres, vertices across it at ±width/2 and
    /// between, each lifted a few centimetres over the final ground height and lit with the ground's normal. UV X runs
    /// across (0 to 1), UV Y is metres along the road, and vertex alpha fades the ends the road should fade at.
    /// </summary>
    public static class MapRoadRibbon
    {
        public const float Step = 2f;
        public const float Lift = 0.05f;
        public const float FadeLength = 4f;

        private const float AcrossStep = 1.5f;
        private const float NormalSpan = 1f;

        /// <summary>
        /// Adds the road's ribbon. <paramref name="groundLevel"/> is the terrain height of world y = 0; rows whose centre is
        /// <paramref name="hidden"/> (over water, under a bridge) are left out and break the ribbon there.
        /// </summary>
        public static void Add(MapRoadMesh mesh, MapRoad road, bool fadeStart, bool fadeEnd, MapHeights heights,
            float groundLevel, Func<Vector2, bool> hidden)
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
                var row = mesh.Count;
                AddRow(mesh, points[i], ahead.normalized, road.Width, columns, along, Mathf.Clamp01(alpha), heights, groundLevel);
                if (previous >= 0)
                {
                    AddQuads(mesh, previous, row, columns);
                }

                previous = row;
            }
        }

        private static void AddRow(MapRoadMesh mesh, Vector2 center, Vector2 forward, float width, int columns,
            float along, float alpha, MapHeights heights, float groundLevel)
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
                mesh.Vertices.Add(new Vector3(point.x, heights.Height(point) - groundLevel + Lift, point.y));
                mesh.Normals.Add(normal);
                mesh.Tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, -1f));
                mesh.Uvs.Add(new Vector2(across, along));
                mesh.Colors.Add(new Color32(255, 255, 255, fade));
            }
        }

        /// <summary>Two triangles per cell, clockwise seen from above.</summary>
        private static void AddQuads(MapRoadMesh mesh, int previous, int row, int columns)
        {
            for (var c = 0; c + 1 < columns; c++)
            {
                var a = previous + c;
                var b = row + c;
                mesh.Triangles.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
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
