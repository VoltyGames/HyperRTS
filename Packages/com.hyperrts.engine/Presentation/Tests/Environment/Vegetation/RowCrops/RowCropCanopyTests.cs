using System.Collections.Generic;
using HyperRTS.Presentation.Environment.Vegetation.RowCrops;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Environment.Vegetation.RowCrops
{
    /// <summary>The canopy cut from a field id picture: its top covers the crop's texels and its walls follow only the outline.</summary>
    public class RowCropCanopyTests
    {
        private const int Size = 80;
        private const float WallTile = 8.4f;

        // A wheat field across the block edge at 64 m and a smaller field of another crop to the north.
        private static readonly RectInt Wheat = new(10, 20, 60, 20);
        private static readonly RectInt Other = new(10, 50, 10, 10);

        private List<RowCropCanopyGeometry> _canopy;

        [SetUp]
        public void SetUp()
        {
            var ids = new int[Size * Size];
            Paint(ids, Wheat, 1);
            Paint(ids, Other, 2);
            var shape = new RowCropCanopyShape(new Rect(0f, 0f, Size, Size), Size, Size, ids, new[] { 0, 1 },
                new float[Size * Size], 64f);
            _canopy = shape.Build(0, WallTile);
        }

        [Test]
        public void Top_CoversTheCropsTexelsAndNoOthers()
        {
            Assert.AreEqual(2, _canopy.Count, "the field spans two blocks");
            for (var j = 0; j < Size; j++)
            {
                for (var i = 0; i < Size; i++)
                {
                    var centre = new Vector2(i + 0.5f, j + 0.5f);
                    // The smoothed outline cuts the field's corner texels.
                    var cornerColumn = i == Wheat.xMin || i == Wheat.xMax - 1;
                    var cornerRow = j == Wheat.yMin || j == Wheat.yMax - 1;
                    if (Wheat.Contains(new Vector2Int(i, j)) && !(cornerColumn && cornerRow))
                    {
                        Assert.IsTrue(OnTop(centre, 1e-3f), $"texel ({i}, {j}) of the crop is uncovered");
                    }
                    else if (!Wheat.Contains(new Vector2Int(i, j)))
                    {
                        Assert.IsFalse(OnTop(centre, -1e-3f), $"texel ({i}, {j}) off the crop is covered");
                    }
                }
            }
        }

        [Test]
        public void Walls_RunOnlyAlongTheOutline_AndCloseIntoWholeTiles()
        {
            var (length, farthest) = (0f, 0f);
            foreach (var geometry in _canopy)
            {
                for (var v = 0; v < geometry.Positions.Count; v++)
                {
                    if (!geometry.OnWall(v))
                    {
                        continue;
                    }

                    var point = geometry.Positions[v];
                    var inside = Mathf.Min(point.x - Wheat.xMin, Wheat.xMax - point.x, point.z - Wheat.yMin, Wheat.yMax - point.z);
                    Assert.LessOrEqual(Mathf.Abs(inside), 0.5f + 1e-3f, $"wall vertex {point} is off the outline");
                    farthest = Mathf.Max(farthest, geometry.Canopy[v].x);
                }

                for (var t = 0; t < geometry.Triangles.Count; t += 3)
                {
                    var (a, b, c) = (geometry.Triangles[t], geometry.Triangles[t + 1], geometry.Triangles[t + 2]);
                    var (pa, pb, pc) = (geometry.Positions[a], geometry.Positions[b], geometry.Positions[c]);
                    Assert.Greater(Vector3.Dot(Vector3.Cross(pb - pa, pc - pa), geometry.Normals[a]), 0f, "a face turns away");
                    if (geometry.OnWall(a) && geometry.Canopy[a].y > 0.5f && geometry.Canopy[b].y > 0.5f)
                    {
                        length += Vector3.Distance(pa, pb);
                    }
                }
            }

            Assert.That(length, Is.InRange(155f, 160f), "the walls run once round the field");
            Assert.AreEqual(Mathf.Round(farthest), farthest, 1e-3f, "the wall loop ends on a whole tile");
        }

        private static void Paint(int[] ids, RectInt rect, int id)
        {
            foreach (var texel in rect.allPositionsWithin)
            {
                ids[texel.y * Size + texel.x] = id;
            }
        }

        // Whether a point falls on a top triangle, no more than margin metres outside any of its edges.
        private bool OnTop(Vector2 point, float margin)
        {
            foreach (var geometry in _canopy)
            {
                for (var t = 0; t < geometry.Triangles.Count; t += 3)
                {
                    if (geometry.OnWall(geometry.Triangles[t]))
                    {
                        continue;
                    }

                    var (a, b, c) = (Flat(geometry, t), Flat(geometry, t + 1), Flat(geometry, t + 2));
                    var sides = new[] { Side(a, b, point), Side(b, c, point), Side(c, a, point) };
                    if (Mathf.Max(sides[0], sides[1], sides[2]) <= margin)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static Vector2 Flat(RowCropCanopyGeometry geometry, int corner)
        {
            var position = geometry.Positions[geometry.Triangles[corner]];
            return new Vector2(position.x, position.z);
        }

        // Positive outside a clockwise-from-above triangle's edge, in metres.
        private static float Side(Vector2 from, Vector2 to, Vector2 point)
        {
            var edge = (to - from).normalized;
            return edge.x * (point.y - from.y) - edge.y * (point.x - from.x);
        }
    }
}
