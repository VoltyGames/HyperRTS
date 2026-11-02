using System.Collections.Generic;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Geometry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Meshes
{
    /// <summary>
    /// River ribbons and pond sheets at water level, tucked under the banks. River UV is in metres (U across, V
    /// downstream) so the shader scrolls with the current; vertex colour marks fords (<see cref="MapWaterFords"/>). A
    /// pond's UV is its map position and its vertex blue marks still water.
    /// </summary>
    public sealed class MapWaterMesh
    {
        private const float Step = 2f;
        private const float BankMargin = 6f;
        private const float EdgeOverhang = 12f;
        private const float SheetMargin = 5f;

        private readonly IReadOnlyList<MapCrossing> _crossings;
        private readonly MapClimate _climate;
        private readonly List<Vector3> _vertices = new();
        private readonly List<Vector2> _uvs = new();
        private readonly List<Color> _colors = new();
        private readonly List<int> _triangles = new();

        private MapWaterMesh(IReadOnlyList<MapCrossing> crossings, MapClimate climate)
        {
            _crossings = crossings;
            _climate = climate;
        }

        /// <summary>River ribbons at <paramref name="height"/>, run on past the terrain's edge (<paramref name="bounds"/>).</summary>
        public static Mesh Build(MapShapes shapes, MapClimate climate, float height, Rect bounds)
        {
            var water = new MapWaterMesh(shapes.Crossings, climate);
            foreach (var river in shapes.Rivers)
            {
                var half = river.Spec.width * 0.5f * (1f + river.Spec.widthVariation) + BankMargin;
                water.AddRibbon(MapGeometry.ExtendAtEdge(river.Line.Points, bounds, EdgeOverhang), half, height);
            }

            foreach (var pond in shapes.Ponds)
            {
                water.AddSheet(pond, height);
            }

            return water.ToMesh();
        }

        /// <summary>Grid cells over the pond and a margin past its edge, which the bank buries.</summary>
        private void AddSheet(MapPond pond, float height)
        {
            var bounds = MapGeometry.Bounds(pond.Shore, SheetMargin);
            var columns = Mathf.CeilToInt(bounds.width / Step) + 1;
            var rows = Mathf.CeilToInt(bounds.height / Step) + 1;
            var first = _vertices.Count;
            var wet = new bool[columns * rows];
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var point = new Vector2(bounds.xMin + column * Step, bounds.yMin + row * Step);
                    _vertices.Add(new Vector3(point.x, height, point.y));
                    _uvs.Add(point);
                    _colors.Add(new Color(0f, 0f, 1f, 0f));
                    wet[row * columns + column] = pond.EdgeDistance(point) < SheetMargin - Step;
                }
            }

            for (var row = 0; row < rows - 1; row++)
            {
                for (var column = 0; column < columns - 1; column++)
                {
                    var i = row * columns + column;
                    if (wet[i] || wet[i + 1] || wet[i + columns] || wet[i + columns + 1])
                    {
                        var a = first + i;
                        _triangles.AddRange(new[] { a, a + columns, a + 1, a + 1, a + columns, a + columns + 1 });
                    }
                }
            }
        }

        private void AddRibbon(List<Vector2> line, float half, float height)
        {
            var length = MapGeometry.Length(line);
            var count = Mathf.Max(2, Mathf.CeilToInt(length / Step) + 1);
            var walker = new MapLineWalker(line);
            var first = _vertices.Count;
            for (var i = 0; i < count; i++)
            {
                var along = Mathf.Min(length, i * Step);
                var point = walker.PointAt(along, out _);
                var ahead = walker.PointAt(Mathf.Min(length, along + Step), out _);
                var behind = walker.PointAt(Mathf.Max(0f, along - Step), out _);
                var direction = (ahead - behind).normalized;
                var left = MapGeometry.Left(direction) * half;
                _vertices.Add(new Vector3(point.x + left.x, height, point.y + left.y));
                _vertices.Add(new Vector3(point.x - left.x, height, point.y - left.y));
                _uvs.Add(new Vector2(-half, along));
                _uvs.Add(new Vector2(half, along));
                var color = MapWaterFords.VertexColor(_crossings, _climate, point, direction);
                _colors.Add(color);
                _colors.Add(color);
                if (i == 0)
                {
                    continue;
                }

                var a = first + (i - 1) * 2;
                _triangles.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
            }
        }

        private Mesh ToMesh()
        {
            var mesh = new Mesh { name = "Water", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(_vertices);
            mesh.SetUVs(0, _uvs);
            mesh.SetColors(_colors);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
