using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment.Horizon
{
    /// <summary>
    /// Builds the tree line, the far belts as one mesh: each run of columns along a belt is a ridge with a crown's
    /// cross-section (straight sides up to a shoulder, then a roof up to the crown's top), closed at both ends.
    /// Normals bend outward and up, so the ridge shades round like a row of crowns. UV y is the height up the crown.
    /// </summary>
    public sealed class HorizonTreeLine
    {
        /// <summary>Share of the half width the shoulders sit in from the sides.</summary>
        private const float ShoulderInset = 0.85f;

        private const int Points = 5;

        private readonly List<Vector3> _vertices = new();
        private readonly List<Vector3> _normals = new();
        private readonly List<Vector2> _uvs = new();
        private readonly List<int>[] _triangles;
        private readonly List<int> _run = new();
        private int _variant = -1;

        public HorizonTreeLine(int variants)
        {
            _triangles = new List<int>[variants];
            for (var i = 0; i < variants; i++)
            {
                _triangles[i] = new List<int>();
            }
        }

        /// <summary>
        /// The next column of a run, standing on <paramref name="ground"/>: <paramref name="shoulder"/> is the share
        /// of <paramref name="height"/> its sides reach before its roof.
        /// </summary>
        public void Column(int variant, Vector3 ground, Vector2 across, float halfWidth, float height, float shoulder)
        {
            if (_variant != variant)
            {
                EndRun();
                _variant = variant;
            }

            var side = new Vector3(across.x, 0f, across.y);
            var first = _vertices.Count;
            Add(ground - side * halfWidth, -side, 0f);
            Add(ground - side * (halfWidth * ShoulderInset) + Vector3.up * (height * shoulder), (Vector3.up - side).normalized, shoulder);
            Add(ground + Vector3.up * height, Vector3.up, 1f);
            Add(ground + side * (halfWidth * ShoulderInset) + Vector3.up * (height * shoulder), (Vector3.up + side).normalized, shoulder);
            Add(ground + side * halfWidth, side, 0f);
            if (_run.Count > 0)
            {
                Join(_run[^1], first);
            }

            _run.Add(first);
        }

        /// <summary>Closes the run's ends; a run of one column is dropped.</summary>
        public void EndRun()
        {
            if (_run.Count > 1)
            {
                var (start, second) = (_run[0], _run[1]);
                var (last, beforeLast) = (_run[^1], _run[^2]);
                Cap(start, _vertices[start + 2] - _vertices[second + 2]);
                Cap(last, _vertices[last + 2] - _vertices[beforeLast + 2]);
            }

            _run.Clear();
        }

        public Mesh Build()
        {
            EndRun();
            var mesh = new Mesh
            {
                name = "Horizon Tree Line",
                hideFlags = HideFlags.DontSave,
                indexFormat = IndexFormat.UInt32,
                subMeshCount = _triangles.Length,
            };
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            for (var i = 0; i < _triangles.Length; i++)
            {
                mesh.SetTriangles(_triangles[i], i, false);
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        private void Add(Vector3 position, Vector3 normal, float up)
        {
            _vertices.Add(position);
            _normals.Add(normal);
            _uvs.Add(new Vector2(0f, up));
        }

        // The four faces between two columns, each facing the way its edge's normals do.
        private void Join(int from, int to)
        {
            for (var k = 0; k < Points - 1; k++)
            {
                var outward = _normals[from + k] + _normals[from + k + 1];
                Triangle(from + k, from + k + 1, to + k + 1, outward);
                Triangle(from + k, to + k + 1, to + k, outward);
            }
        }

        private void Cap(int column, Vector3 outward)
        {
            for (var k = 1; k < Points - 1; k++)
            {
                Triangle(column, column + k, column + k + 1, outward);
            }
        }

        // Wound clockwise seen from the outward side, as Unity's front faces are.
        private void Triangle(int a, int b, int c, Vector3 outward)
        {
            var normal = Vector3.Cross(_vertices[b] - _vertices[a], _vertices[c] - _vertices[a]);
            var triangles = _triangles[_variant];
            triangles.Add(a);
            if (Vector3.Dot(normal, outward) >= 0f)
            {
                triangles.Add(b);
                triangles.Add(c);
            }
            else
            {
                triangles.Add(c);
                triangles.Add(b);
            }
        }
    }
}
