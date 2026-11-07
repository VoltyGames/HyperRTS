using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment.Horizon
{
    /// <summary>
    /// The horizon's ground mesh: a ring round the terrain whose loops spread further apart with distance.
    /// </summary>
    public static class HorizonRing
    {
        /// <summary>Metres the first loop reaches in under the terrain, so no crack shows at its edge.</summary>
        public const float Tuck = 4f;

        /// <summary>How far below the terrain the tucked loop lies, and the edge loop just under it.</summary>
        private const float TuckDepth = 0.6f;
        private const float EdgeDrop = 0.1f;

        /// <summary>Metres between vertices along the terrain's sides, fine enough to follow its edge heights.</summary>
        private const float SideSpacing = 4f;

        private const int ArcSteps = 24;
        private const float FirstStep = 8f;
        private const float StepGrowth = 1.3f;

        /// <summary>Each loop's distance past the terrain's edge: under it, on it, then growing steps out to the width.</summary>
        public static List<float> Offsets(float width)
        {
            var offsets = new List<float> { -Tuck, 0f };
            var (offset, step) = (FirstStep, FirstStep);
            while (offset < width)
            {
                offsets.Add(offset);
                step *= StepGrowth;
                offset += step;
            }

            offsets.Add(width);
            return offsets;
        }

        /// <summary>
        /// The ring round <paramref name="terrain"/>: <paramref name="edgeHeight"/> gives the terrain's own height at a
        /// point inside it, <paramref name="height"/> the land's height outside.
        /// </summary>
        public static Mesh Build(Rect terrain, float width, Func<Vector2, float> edgeHeight, Func<Vector2, float> height)
        {
            var offsets = Offsets(width);
            var sides = new[]
            {
                Steps(terrain.width), Steps(terrain.height), Steps(terrain.width), Steps(terrain.height),
            };
            var loop = sides[0] + sides[1] + sides[2] + sides[3] + 4 * ArcSteps;
            var vertices = new List<Vector3>(offsets.Count * loop);
            foreach (var offset in offsets)
            {
                foreach (var point in Loop(terrain, offset, sides))
                {
                    var y = offset switch
                    {
                        < 0f => edgeHeight(point) - TuckDepth,
                        0f => edgeHeight(point) - EdgeDrop,
                        _ => height(point),
                    };
                    vertices.Add(new Vector3(point.x, y, point.y));
                }
            }

            var triangles = new List<int>(6 * (offsets.Count - 1) * loop);
            for (var ring = 0; ring < offsets.Count - 1; ring++)
            {
                for (var i = 0; i < loop; i++)
                {
                    var (inner, innerNext) = (ring * loop + i, ring * loop + (i + 1) % loop);
                    var (outer, outerNext) = (inner + loop, innerNext + loop);
                    triangles.AddRange(new[] { inner, innerNext, outerNext, inner, outerNext, outer });
                }
            }

            var mesh = new Mesh
            {
                name = "Horizon Ground",
                hideFlags = HideFlags.DontSave,
                indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16,
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            FaceTuckUp(mesh, loop);
            mesh.RecalculateBounds();
            return mesh;
        }

        // The loops under and on the terrain's edge have no rounding, so the faces between their corners have no area
        // and the tucked loop's corner vertices get no normal. The whole loop lies under the terrain, so it faces up.
        private static void FaceTuckUp(Mesh mesh, int loop)
        {
            var normals = mesh.normals;
            for (var i = 0; i < loop; i++)
            {
                normals[i] = Vector3.up;
            }

            mesh.normals = normals;
        }

        /// <summary>How far a point lies past the terrain's edge; 0 on or inside it.</summary>
        public static float Distance(Rect terrain, Vector2 point) =>
            (point - Vector2.Max(terrain.min, Vector2.Min(terrain.max, point))).magnitude;

        private static int Steps(float length) => Mathf.Max(1, Mathf.CeilToInt(length / SideSpacing));

        // One loop counter-clockwise from the south-west corner: each side's steps, then its corner's arc. Inside the
        // terrain the loop is the rectangle shrunk; outside it, the rectangle grown with rounded corners.
        private static IEnumerable<Vector2> Loop(Rect terrain, float offset, int[] sides)
        {
            var inset = Mathf.Max(0f, -offset);
            var radius = Mathf.Max(0f, offset);
            var (min, max) = (terrain.min + Vector2.one * inset, terrain.max - Vector2.one * inset);
            var corners = new[] { min, new Vector2(max.x, min.y), max, new Vector2(min.x, max.y) };
            for (var side = 0; side < 4; side++)
            {
                // South faces -90 degrees; each side turns a quarter further.
                var facing = (side - 1) * 90f;
                var normal = Direction(facing);
                var (from, to) = (corners[side], corners[(side + 1) % 4]);
                for (var step = 0; step < sides[side]; step++)
                {
                    yield return Vector2.Lerp(from, to, step / (float)sides[side]) + normal * radius;
                }

                for (var step = 0; step < ArcSteps; step++)
                {
                    yield return to + Direction(facing + 90f * step / ArcSteps) * radius;
                }
            }
        }

        private static Vector2 Direction(float degrees) =>
            new(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
    }
}
