using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Vegetation.RowCrops
{
    /// <summary>
    /// Cuts a crop's canopy from the field id picture: marching squares over a 3 by 3 smoothed mask of the crop's
    /// texels, so outlines run diagonally rather than in texel steps, in blocks of a set size. The top lies on the
    /// ground for the shader to raise; walls hang from the outline to under the ground.
    /// </summary>
    public sealed class RowCropCanopyShape
    {
        /// <summary>Mask value the outline follows.</summary>
        public const float Edge = 0.5f;

        /// <summary>Metres a wall reaches under the ground, so no gap shows on a slope or in a crater.</summary>
        public const float WallDepth = 0.3f;

        // The texel itself first, then its neighbours.
        private static readonly int[] Offsets = { 0, -1, 1 };

        private readonly int _width;
        private readonly int _height;
        private readonly Vector2 _first;
        private readonly Vector2 _cell;
        private readonly int[] _ids;
        private readonly IReadOnlyList<int> _fieldCrops;
        private readonly float[] _heights;
        private readonly int _block;
        private readonly float[] _mask;
        private readonly int[] _vertices;
        private int _crop;

        /// <summary>
        /// A shape over <paramref name="ids"/>, the picture's field ids row by row from the south, with
        /// <paramref name="heights"/> the ground under each texel's centre and <paramref name="fieldCrops"/> each field's crop.
        /// </summary>
        public RowCropCanopyShape(Rect area, int width, int height, int[] ids, IReadOnlyList<int> fieldCrops,
            float[] heights, float blockSize)
        {
            _width = width;
            _height = height;
            _cell = new Vector2(area.width / width, area.height / height);
            _first = area.min + _cell * 0.5f;
            _ids = ids;
            _fieldCrops = fieldCrops;
            _heights = heights;
            _block = Mathf.Max(1, Mathf.RoundToInt(blockSize / _cell.x));
            _mask = new float[width * height];
            _vertices = new int[3 * (_block + 1) * (_block + 1)];
        }

        /// <summary>
        /// The canopy of <paramref name="crop"/>, one geometry per block it grows in, with wall textures
        /// <paramref name="wallTile"/> metres long.
        /// </summary>
        public List<RowCropCanopyGeometry> Build(int crop, float wallTile)
        {
            _crop = crop;
            FillMask();
            var (across, deep) = (Blocks(_width), Blocks(_height));
            var blocks = new RowCropCanopyGeometry[across * deep];
            var outline = new RowCropCanopyOutline();
            for (var b = 0; b < blocks.Length; b++)
            {
                var (i0, j0) = (b % across * _block, b / across * _block);
                if (AnyInside(i0, j0))
                {
                    blocks[b] = new RowCropCanopyGeometry();
                    AddTop(blocks[b], b, i0, j0, outline);
                }
            }

            outline.Measure(wallTile);
            for (var s = 0; s < outline.Count; s++)
            {
                AddWall(blocks[outline[s].Block], outline[s]);
            }

            return new List<RowCropCanopyGeometry>(Array.FindAll(blocks, block => block != null));
        }

        private int Blocks(int texels) => Mathf.CeilToInt((texels - 1) / (float)_block);

        private bool Grows(int texel)
        {
            var id = _ids[texel];
            return id != RowCropFields.None && _fieldCrops[id - 1] == _crop;
        }

        private bool InPicture(int i, int j) => (uint)i < (uint)_width && (uint)j < (uint)_height;

        private float Sown(int i, int j) => InPicture(i, j) && Grows(j * _width + i) ? 1f : 0f;

        private bool Inside(int lattice) => _mask[lattice] >= Edge;

        // A 3 by 3 box sum: a straight edge keeps its place and a texel step turns into a diagonal.
        private void FillMask()
        {
            var rows = new float[_mask.Length];
            for (var p = 0; p < rows.Length; p++)
            {
                var (i, j) = (p % _width, p / _width);
                rows[p] = Sown(i - 1, j) + Sown(i, j) + Sown(i + 1, j);
            }

            for (var p = 0; p < rows.Length; p++)
            {
                var j = p / _width;
                var below = j > 0 ? rows[p - _width] : 0f;
                var above = j < _height - 1 ? rows[p + _width] : 0f;
                _mask[p] = (below + rows[p] + above) / 9f;
            }
        }

        private bool AnyInside(int i0, int j0)
        {
            for (var j = j0; j <= Mathf.Min(j0 + _block, _height - 1); j++)
            {
                for (var i = i0; i <= Mathf.Min(i0 + _block, _width - 1); i++)
                {
                    if (Inside(j * _width + i))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void AddTop(RowCropCanopyGeometry geometry, int block, int i0, int j0, RowCropCanopyOutline outline)
        {
            Array.Fill(_vertices, -1);
            var polygon = new int[6];
            for (var j = j0; j < Mathf.Min(j0 + _block, _height - 1); j++)
            {
                for (var i = i0; i < Mathf.Min(i0 + _block, _width - 1); i++)
                {
                    var count = Polygon(j * _width + i, polygon);
                    if (count == 0)
                    {
                        continue;
                    }

                    if (count == 6 && Separated(j * _width + i))
                    {
                        // The saddle's two inside corners each keep a triangle of their own.
                        var corner = polygon[0] < 0 ? 0 : 1;
                        var first = new[] { polygon[(corner + 5) % 6], polygon[corner], polygon[corner + 1] };
                        var second = new[] { polygon[corner + 2], polygon[corner + 3], polygon[corner + 4] };
                        AddPolygon(geometry, block, i0, j0, outline, first, 3);
                        AddPolygon(geometry, block, i0, j0, outline, second, 3);
                        continue;
                    }

                    AddPolygon(geometry, block, i0, j0, outline, polygon, count);
                }
            }
        }

        // The square's crop part, counterclockwise from above: corners as -(lattice + 1), crossings as edge ids.
        private int Polygon(int p0, int[] polygon)
        {
            Span<int> corners = stackalloc int[] { p0, p0 + 1, p0 + _width + 1, p0 + _width };
            Span<int> edges = stackalloc int[] { 2 * p0, 2 * (p0 + 1) + 1, 2 * (p0 + _width), 2 * p0 + 1 };
            var count = 0;
            for (var k = 0; k < 4; k++)
            {
                var inside = Inside(corners[k]);
                if (inside)
                {
                    polygon[count++] = -(corners[k] + 1);
                }

                if (inside != Inside(corners[(k + 1) % 4]))
                {
                    polygon[count++] = edges[k];
                }
            }

            return count;
        }

        // A saddle whose centre falls outside parts its two inside corners.
        private bool Separated(int p0)
        {
            var centre = (_mask[p0] + _mask[p0 + 1] + _mask[p0 + _width] + _mask[p0 + _width + 1]) * 0.25f;
            return centre < Edge;
        }

        // Fanned clockwise from above, as Unity's front faces run; each cut between two crossings is outline.
        private void AddPolygon(RowCropCanopyGeometry geometry, int block, int i0, int j0, RowCropCanopyOutline outline,
            int[] polygon, int count)
        {
            Span<int> vertices = stackalloc int[count];
            for (var k = 0; k < count; k++)
            {
                vertices[k] = TopVertex(geometry, polygon[k], i0, j0);
            }

            for (var k = 1; k < count - 1; k++)
            {
                geometry.AddTriangle(vertices[0], vertices[k + 1], vertices[k]);
            }

            for (var k = 0; k < count; k++)
            {
                var (from, to) = (polygon[k], polygon[(k + 1) % count]);
                if (from >= 0 && to >= 0)
                {
                    outline.Add(block, from, to, Vector3.Distance(Crossing(from).Position, Crossing(to).Position));
                }
            }
        }

        private int TopVertex(RowCropCanopyGeometry geometry, int code, int i0, int j0)
        {
            var lattice = code < 0 ? -code - 1 : code >> 1;
            var local = (lattice / _width - j0) * (_block + 1) + lattice % _width - i0;
            var slot = 3 * local + (code < 0 ? 0 : 1 + (code & 1));
            if (_vertices[slot] < 0)
            {
                var point = code < 0 ? Corner(lattice, NearestField(lattice)) : Crossing(code);
                _vertices[slot] = geometry.Add(point.Position, point.Normal, new Vector4(0f, 1f, 0f, point.Field));
            }

            return _vertices[slot];
        }

        private void AddWall(RowCropCanopyGeometry geometry, RowCropCanopyOutline.Segment segment)
        {
            var (a, b) = (Crossing(segment.From), Crossing(segment.To));
            var run = b.Position - a.Position;
            if (segment.Length < 1e-4f)
            {
                return;
            }

            // Outward is to the right of the run, as the crop lies on its left.
            var normal = new Vector3(run.z, 0f, -run.x).normalized;
            var down = Vector3.down * WallDepth;
            var aTop = geometry.Add(a.Position, normal, new Vector4(segment.Start, 1f, 1f, a.Field));
            var bTop = geometry.Add(b.Position, normal, new Vector4(segment.End, 1f, 1f, b.Field));
            var bFoot = geometry.Add(b.Position + down, normal, new Vector4(segment.End, 0f, 1f, b.Field));
            var aFoot = geometry.Add(a.Position + down, normal, new Vector4(segment.Start, 0f, 1f, a.Field));
            geometry.AddTriangle(aTop, bTop, bFoot);
            geometry.AddTriangle(aTop, bFoot, aFoot);
        }

        private (Vector3 Position, Vector3 Normal, int Field) Corner(int lattice, int field)
        {
            var (i, j) = (lattice % _width, lattice / _width);
            var position = new Vector3(_first.x + i * _cell.x, _heights[lattice], _first.y + j * _cell.y);
            return (position, GroundNormal(i, j), field);
        }

        // Where the outline crosses a lattice edge; it takes the field of the edge's inside end.
        private (Vector3 Position, Vector3 Normal, int Field) Crossing(int edge)
        {
            var a = edge >> 1;
            var b = (edge & 1) == 0 ? a + 1 : a + _width;
            var t = (Edge - _mask[a]) / (_mask[b] - _mask[a]);
            var field = NearestField(Inside(a) ? a : b);
            var (start, end) = (Corner(a, field), Corner(b, field));
            return (Vector3.Lerp(start.Position, end.Position, t), Vector3.Lerp(start.Normal, end.Normal, t).normalized, field);
        }

        private Vector3 GroundNormal(int i, int j)
        {
            var (west, east) = (Mathf.Max(i - 1, 0), Mathf.Min(i + 1, _width - 1));
            var (south, north) = (Mathf.Max(j - 1, 0), Mathf.Min(j + 1, _height - 1));
            var slopeX = (_heights[j * _width + east] - _heights[j * _width + west]) / ((east - west) * _cell.x);
            var slopeZ = (_heights[north * _width + i] - _heights[south * _width + i]) / ((north - south) * _cell.y);
            return new Vector3(-slopeX, 1f, -slopeZ).normalized;
        }

        // An inside lattice point has the crop in at least five of its nine texels, so one is always found.
        private int NearestField(int lattice)
        {
            var (i, j) = (lattice % _width, lattice / _width);
            foreach (var dj in Offsets)
            {
                foreach (var di in Offsets)
                {
                    if (Sown(i + di, j + dj) > 0f)
                    {
                        return _ids[(j + dj) * _width + i + di];
                    }
                }
            }

            throw new InvalidOperationException($"HyperRTS: canopy lattice point ({i}, {j}) has no texel of crop {_crop} round it.");
        }
    }
}
