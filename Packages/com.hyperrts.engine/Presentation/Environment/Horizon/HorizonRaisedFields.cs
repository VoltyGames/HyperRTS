using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment.Horizon
{
    /// <summary>
    /// The horizon's raised crop fields: over each field whose crop has a canopy, a box at the crop's height inside its
    /// margins, walls round it, following the land. One submesh per raised crop, drawn by HyperRTS/HorizonCrop in the
    /// crop's canopy textures.
    /// </summary>
    [Serializable]
    public sealed class HorizonRaisedFields
    {
        // Where a pixel spans this share of a cell's width the crop has faded to its mean, as the ground does.
        private const float MeanFootprint = 0.3f;

        private static readonly int TileId = Shader.PropertyToID("_CanopyTile");
        private static readonly int GridTurnId = Shader.PropertyToID("_GridTurn");
        private static readonly int MeanFootprintId = Shader.PropertyToID("_MeanFootprint");

        [Tooltip("HyperRTS/HorizonCrop, which draws the raised fields in their crops' canopy textures.")]
        public Shader shader;

        /// <summary>The crops whose fields stand raised, by index in <see cref="HorizonFields.crops"/>: one submesh each.</summary>
        public List<int> RaisedCrops(HorizonFields fields) =>
            Enumerable.Range(0, fields.crops.Count).Where(i => fields.crops[i].Raised).ToList();

        /// <summary>What keeps the raised fields from drawing, one entry per problem.</summary>
        public IEnumerable<string> Problems(HorizonFields fields)
        {
            if (shader == null && RaisedCrops(fields).Count > 0)
            {
                yield return "no raised crop shader";
            }
        }

        /// <summary>The raised fields round <paramref name="terrain"/>: wholly past it and inside the ring.</summary>
        public List<HorizonRaisedField> Fields(HorizonFields fields, int seed, Rect terrain, float width)
        {
            var raised = new List<HorizonRaisedField>();
            var area = Rect.MinMaxRect(terrain.xMin - width, terrain.yMin - width, terrain.xMax + width, terrain.yMax + width);
            var cells = fields.Cells(area);
            for (var row = cells.yMin; row < cells.yMax; row++)
            {
                for (var part = 0; part < 2; part++)
                {
                    AddRow(raised, fields, seed, cells, row, part);
                }
            }

            raised.RemoveAll(field => !Fits(fields, field.area, terrain, width));
            return raised;
        }

        /// <summary>The boxes, one submesh per crop of <see cref="RaisedCrops"/>, which the caller destroys.</summary>
        public Mesh Build(List<HorizonRaisedField> raised, HorizonFields fields, Func<Vector2, float> height)
        {
            var crops = RaisedCrops(fields);
            var (vertices, canopy) = (new List<Vector3>(), new List<Vector4>());
            var triangles = crops.Select(_ => new List<int>()).ToList();
            foreach (var field in raised)
            {
                var crop = fields.crops[field.crop].crop;
                var box = new HorizonRaisedBox(fields, height, crop.height, crop.CanopyTile.y, vertices, canopy, triangles[crops.IndexOf(field.crop)]);
                box.AddTop(field.area);
                box.AddWalls(field.area);
            }

            var mesh = new Mesh
            {
                name = "Horizon Raised Fields",
                hideFlags = HideFlags.DontSave,
                indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16,
                subMeshCount = crops.Count,
            };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, canopy);
            for (var i = 0; i < crops.Count; i++)
            {
                mesh.SetTriangles(triangles[i], i);
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>One material per crop of <see cref="RaisedCrops"/>, in its canopy's textures; the caller destroys them.</summary>
        public List<Material> CreateMaterials(HorizonFields fields)
        {
            var radians = fields.angle * Mathf.Deg2Rad;
            return RaisedCrops(fields).Select(index =>
            {
                var crop = fields.crops[index].crop;
                var material = new Material(shader) { name = $"Horizon {crop.name}", hideFlags = HideFlags.DontSave };
                material.CopyMatchingPropertiesFromMaterial(crop.canopyMaterial);
                material.SetVector(TileId, new Vector4(1f / crop.CanopyTile.x, 1f / crop.CanopyTile.y, 0f, 0f));
                material.SetVector(GridTurnId, new Vector4(Mathf.Cos(radians), Mathf.Sin(radians), 0f, 0f));
                material.SetFloat(MeanFootprintId, Mathf.Min(fields.size.x, fields.size.y) * MeanFootprint);
                return material;
            }).ToList();
        }

        // Each distinct field of a row part once: a merged field from its first cell, a split cell's halves both.
        private static void AddRow(List<HorizonRaisedField> raised, HorizonFields fields, int seed, RectInt cells, int row, int part)
        {
            var line = fields.Row(seed, row, part);
            var across = new Vector2(line.start + line.southMargin, line.end - line.northMargin);
            for (var column = cells.xMin - 1; column <= cells.xMax; column++)
            {
                for (var cellPart = 0; cellPart < 2; cellPart++)
                {
                    var field = fields.Field(seed, row, part, column, cellPart);
                    var startsHere = field.start >= column * fields.size.x;
                    var repeated = cellPart == 1 && field.start == fields.Field(seed, row, part, column, 0).start;
                    if (!startsHere || repeated || !fields.crops[field.crop].Raised)
                    {
                        continue;
                    }

                    var along = new Vector2(field.start + field.startMargin, field.end - field.endMargin) - Vector2.one * line.slide;
                    if (along.y > along.x && across.y > across.x)
                    {
                        raised.Add(new HorizonRaisedField { area = Rect.MinMaxRect(along.x, across.x, along.y, across.y), crop = field.crop });
                    }
                }
            }
        }

        // Wholly past the terrain and inside the ring. Two crossing rectangles share a corner or a long run of edge, so
        // the corners and edge points a step apart tell.
        private static bool Fits(HorizonFields fields, Rect area, Rect terrain, float width)
        {
            var terrainCorners = new[] { terrain.min, terrain.max, new Vector2(terrain.xMin, terrain.yMax), new Vector2(terrain.xMax, terrain.yMin) };
            if (terrainCorners.Any(corner => area.Contains(fields.ToGrid(corner))))
            {
                return false;
            }

            foreach (var point in Perimeter(area))
            {
                var distance = HorizonRing.Distance(terrain, fields.ToWorld(point));
                if (distance <= 0f || distance > width)
                {
                    return false;
                }
            }

            return true;
        }

        private static IEnumerable<Vector2> Perimeter(Rect area)
        {
            var corners = HorizonRaisedBox.Corners(area);
            for (var side = 0; side < 4; side++)
            {
                var (from, to) = (corners[side], corners[(side + 1) % 4]);
                var steps = HorizonRaisedBox.Steps(Vector2.Distance(from, to));
                for (var i = 0; i < steps; i++)
                {
                    yield return Vector2.Lerp(from, to, i / (float)steps);
                }
            }
        }
    }
}
