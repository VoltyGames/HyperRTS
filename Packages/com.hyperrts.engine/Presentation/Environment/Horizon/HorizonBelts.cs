using HyperRTS.Simulation.Common.Authoring;
using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Environment.Vegetation.Scatter;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment.Horizon
{
    /// <summary>
    /// Tree lines along some long row edges of the horizon, drawn as staggered strips of upright quads.
    /// </summary>
    [Serializable]
    public sealed class HorizonBelts
    {
        private const uint BeltSalt = 0x62u;
        private const uint StripSalt = 0x70u;

        // Short enough for the quads to follow the swells.
        private const float QuadLength = 8f;

        // The strip's foot sits this far under the ground, so no light shows beneath it on a slope.
        private const float Sink = 0.3f;

        private static readonly int StripSizeId = Shader.PropertyToID("_StripSize");

        [Tooltip("Share of the long row edges without a track that carry a belt.")]
        [Range(0f, 1f)]
        public float share = 0.3f;

        [Tooltip("Metres past the terrain's edge before any belt stands, so none comes close to the camera.")]
        [Min(0f)]
        public float clearance = 150f;

        [Tooltip("Metres between a belt's two layers.")]
        [Min(0f)]
        public float depth = 3f;

        [Tooltip("Width of the gap where a track crosses a belt.")]
        [Unit("m")]
        [Min(0f)]
        public float gapWidth = 12f;

        [Tooltip("HyperRTS/HorizonBelt materials, one picked per stretch; empty for no belts.")]
        public List<Material> strips = new();

        /// <summary>What keeps the strips from drawing, one entry per problem.</summary>
        public IEnumerable<string> Problems()
        {
            foreach (var strip in strips)
            {
                if (strip == null)
                {
                    yield return "a belt strip material is missing";
                    continue;
                }

                var size = strip.HasVector(StripSizeId) ? strip.GetVector(StripSizeId) : Vector4.zero;
                if (size.x <= 0f || size.y <= 0f)
                {
                    yield return $"the belt strip '{strip.name}' has no _StripSize";
                }
            }
        }

        /// <summary>The belts round <paramref name="terrain"/> out to <paramref name="width"/>, on the fields' row edges.</summary>
        public List<HorizonBeltStretch> Stretches(HorizonFields fields, int seed, Rect terrain, float width)
        {
            var stretches = new List<HorizonBeltStretch>();
            if (strips.Count == 0)
            {
                return stretches;
            }

            var area = Rect.MinMaxRect(terrain.xMin - width, terrain.yMin - width, terrain.xMax + width, terrain.yMax + width);
            var cells = fields.Cells(area);
            for (var row = cells.yMin; row <= cells.yMax; row++)
            {
                for (var part = 0; part < 2; part++)
                {
                    if (Carries(fields, seed, row, part))
                    {
                        AddEdge(stretches, fields, seed, terrain, width, cells, row, part);
                    }
                }
            }

            return stretches;
        }

        /// <summary>The stretches' quads, one submesh per strip material, which the caller destroys.</summary>
        public Mesh Build(List<HorizonBeltStretch> stretches, Func<Vector2, float> height)
        {
            var (vertices, normals, tangents, uvs) = (new List<Vector3>(), new List<Vector3>(), new List<Vector4>(), new List<Vector2>());
            var triangles = strips.Select(_ => new List<int>()).ToList();
            foreach (var stretch in stretches)
            {
                AddStretch(stretch, height, vertices, normals, tangents, uvs, triangles[stretch.strip]);
            }

            var mesh = new Mesh
            {
                name = "Horizon Belts",
                hideFlags = HideFlags.DontSave,
                indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16,
                subMeshCount = strips.Count,
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uvs);
            for (var i = 0; i < strips.Count; i++)
            {
                mesh.SetTriangles(triangles[i], i);
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        private void AddStretch(HorizonBeltStretch stretch, Func<Vector2, float> height, List<Vector3> vertices,
            List<Vector3> normals, List<Vector4> tangents, List<Vector2> uvs, List<int> triangles)
        {
            var size = (Vector2)strips[stretch.strip].GetVector(StripSizeId);
            var line = stretch.to - stretch.from;
            var (length, along) = (line.magnitude, line.normalized);
            var side = new Vector2(-along.y, along.x);
            var quads = Mathf.Max(1, Mathf.RoundToInt(length / QuadLength));
            for (var layer = 0; layer < 2; layer++)
            {
                var first = vertices.Count;
                for (var i = 0; i <= quads; i++)
                {
                    var distance = length * i / quads;
                    var point = stretch.from + along * distance + side * (depth * (layer - 0.5f));
                    var ground = height(point) - Sink;
                    // The layers start half a strip apart, so their gaps don't line up.
                    var u = distance + layer * size.x * 0.5f;
                    vertices.Add(new Vector3(point.x, ground, point.y));
                    vertices.Add(new Vector3(point.x, ground + Sink + size.y, point.y));
                    uvs.Add(new Vector2(u, 0f));
                    uvs.Add(new Vector2(u, 1f));
                    normals.AddRange(Enumerable.Repeat(new Vector3(side.x, 0f, side.y), 2));
                    tangents.AddRange(Enumerable.Repeat(new Vector4(along.x, 0f, along.y, 1f), 2));
                }

                for (var i = 0; i < quads; i++)
                {
                    var bottom = first + 2 * i;
                    triangles.AddRange(new[] { bottom, bottom + 1, bottom + 3, bottom, bottom + 3, bottom + 2 });
                }
            }
        }

        // A row's south edge: part 1's only where the grid row splits; never along a track.
        private bool Carries(HorizonFields fields, int seed, int row, int part)
        {
            if (part == 1 && !fields.Splits(seed, row))
            {
                return false;
            }

            if (fields.EdgeTrack(seed, row, part))
            {
                return false;
            }

            return Hashes.Fraction(ScatterHash.Hash(ScatterHash.Salted(seed, BeltSalt), row, part)) < share;
        }

        // Walks the edge a quad at a time, opening a stretch where quads may stand and closing it where they can't.
        private void AddEdge(List<HorizonBeltStretch> stretches, HorizonFields fields, int seed, Rect terrain, float width,
            RectInt cells, int row, int part)
        {
            var y = fields.Row(seed, row, part).start;
            var gaps = Crossings(fields, seed, cells, row, part);
            var (open, start) = (false, 0f);
            var end = (cells.xMax + 1) * fields.size.x;
            for (var x = (cells.xMin - 1) * fields.size.x; x < end + QuadLength; x += QuadLength)
            {
                var stands = x < end && Stands(fields, terrain, width, gaps, x, y);
                if (stands == open)
                {
                    continue;
                }

                if (stands)
                {
                    (open, start) = (true, x);
                    continue;
                }

                open = false;
                var pick = ScatterHash.Hash(ScatterHash.Salted(seed, StripSalt), row * 2 + part, Mathf.RoundToInt(start));
                stretches.Add(new HorizonBeltStretch
                {
                    from = fields.ToWorld(new Vector2(start, y)),
                    to = fields.ToWorld(new Vector2(x, y)),
                    strip = Mathf.Min(strips.Count - 1, (int)(Hashes.Fraction(pick) * strips.Count)),
                });
            }
        }

        private bool Stands(HorizonFields fields, Rect terrain, float width, List<float> gaps, float x, float y)
        {
            if (!InRing(fields, terrain, width, x, y) || !InRing(fields, terrain, width, x + QuadLength, y))
            {
                return false;
            }

            var half = gapWidth * 0.5f;
            foreach (var gap in gaps)
            {
                if (gap > x - half && gap < x + QuadLength + half)
                {
                    return false;
                }
            }

            return true;
        }

        private bool InRing(HorizonFields fields, Rect terrain, float width, float x, float y)
        {
            var distance = HorizonRing.Distance(terrain, fields.ToWorld(new Vector2(x, y)));
            return distance >= clearance && distance <= width;
        }

        // Grid x of every field-end track meeting the edge, from the rows on both its sides.
        private static List<float> Crossings(HorizonFields fields, int seed, RectInt cells, int row, int part)
        {
            var south = part == 1 ? (row, 0) : (row - 1, fields.Splits(seed, row - 1) ? 1 : 0);
            var crossings = new List<float>();
            foreach (var (sideRow, sidePart) in new[] { south, (row, part) })
            {
                var slide = fields.Row(seed, sideRow, sidePart).slide;
                for (var column = cells.xMin - 1; column <= cells.xMax + 1; column++)
                {
                    var cellStart = column * fields.size.x;
                    var fieldEnd = fields.Field(seed, sideRow, sidePart, column, 0).start == cellStart;
                    if (fieldEnd && fields.EndTrack(seed, sideRow, sidePart, column))
                    {
                        crossings.Add(cellStart - slide);
                    }
                }
            }

            return crossings;
        }
    }
}
