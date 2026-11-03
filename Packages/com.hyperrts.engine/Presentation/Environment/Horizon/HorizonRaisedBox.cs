using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Horizon
{
    /// <summary>
    /// Writes one raised field's box into HorizonRaisedFields' mesh lists: a top at the crop's height over the land and
    /// walls from under the ground up to it. Per vertex canopy values as HyperRTS/HorizonCrop reads them: x wall tiles
    /// along the outline, y share up the wall (1 on the top), z 1 on walls.
    /// </summary>
    internal sealed class HorizonRaisedBox
    {
        // Short enough for the top to follow the swells.
        private const float Step = 32f;

        // RowCropCanopyShape.WallDepth: walls reach under the ground, so no gap shows on a slope.
        private const float Sink = 0.3f;

        private readonly HorizonFields _fields;
        private readonly Func<Vector2, float> _height;
        private readonly float _cropHeight;
        private readonly float _wallTile;
        private readonly List<Vector3> _vertices;
        private readonly List<Vector4> _canopy;
        private readonly List<int> _triangles;

        public HorizonRaisedBox(HorizonFields fields, Func<Vector2, float> height, float cropHeight, float wallTile,
            List<Vector3> vertices, List<Vector4> canopy, List<int> triangles)
        {
            (_fields, _height, _cropHeight, _wallTile) = (fields, height, cropHeight, wallTile);
            (_vertices, _canopy, _triangles) = (vertices, canopy, triangles);
        }

        /// <summary>A grid rectangle's corners counter-clockwise seen from above, from its south-west.</summary>
        public static Vector2[] Corners(Rect area) =>
            new[] { area.min, new Vector2(area.xMax, area.yMin), area.max, new Vector2(area.xMin, area.yMax) };

        /// <summary>Quads along a run of this many metres.</summary>
        public static int Steps(float length) => Mathf.Max(1, Mathf.CeilToInt(length / Step));

        public void AddTop(Rect area)
        {
            var (columns, rows) = (Steps(area.width), Steps(area.height));
            var first = _vertices.Count;
            for (var j = 0; j <= rows; j++)
            {
                for (var i = 0; i <= columns; i++)
                {
                    var grid = new Vector2(area.xMin + area.width * i / columns, area.yMin + area.height * j / rows);
                    AddVertex(grid, _cropHeight, new Vector4(0f, 1f, 0f, 0f));
                }
            }

            for (var j = 0; j < rows; j++)
            {
                for (var i = 0; i < columns; i++)
                {
                    var a = first + j * (columns + 1) + i;
                    var (b, d) = (a + 1, a + columns + 1);
                    // Clockwise from above, Unity's front face.
                    _triangles.AddRange(new[] { a, d, d + 1, a, d + 1, b });
                }
            }
        }

        public void AddWalls(Rect area)
        {
            var corners = Corners(area);
            var along = 0f;
            for (var side = 0; side < 4; side++)
            {
                var (from, to) = (corners[side], corners[(side + 1) % 4]);
                var length = Vector2.Distance(from, to);
                var steps = Steps(length);
                var first = _vertices.Count;
                for (var i = 0; i <= steps; i++)
                {
                    var u = (along + length * i / steps) / _wallTile;
                    var grid = Vector2.Lerp(from, to, i / (float)steps);
                    AddVertex(grid, -Sink, new Vector4(u, 0f, 1f, 0f));
                    AddVertex(grid, _cropHeight, new Vector4(u, 1f, 1f, 0f));
                }

                for (var i = 0; i < steps; i++)
                {
                    var bottom = first + 2 * i;
                    // Seen from outside the box, its run goes left to right: clockwise is up, across, down.
                    _triangles.AddRange(new[] { bottom, bottom + 1, bottom + 3, bottom, bottom + 3, bottom + 2 });
                }

                along += length;
            }
        }

        private void AddVertex(Vector2 grid, float rise, Vector4 canopy)
        {
            var world = _fields.ToWorld(grid);
            _vertices.Add(new Vector3(world.x, _height(world) + rise, world.y));
            _canopy.Add(canopy);
        }
    }
}
