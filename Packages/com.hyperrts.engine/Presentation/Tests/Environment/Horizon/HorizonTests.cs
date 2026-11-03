using System.Collections.Generic;
using System.Linq;
using HyperRTS.Presentation.Environment.Horizon;
using HyperRTS.Presentation.Environment.Vegetation.RowCrops;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Environment.Horizon
{
    /// <summary>
    /// The horizon's ring reaches from under the terrain out to its width, its fields tile the farmland in varied sizes
    /// with tracks only on their edges, its belts stand on row edges clear of the terrain, and only canopy crops stand
    /// raised, inside their margins.
    /// </summary>
    public class HorizonTests
    {
        private const int Seed = 30871;
        private const float Width = 1000f;
        private static readonly Rect Terrain = new(-420f, -420f, 840f, 840f);
        private static readonly Rect Area = new(-1420f, -1420f, 2840f, 2840f);

        private static HorizonFields Fields() => new()
        {
            size = new Vector2(272f, 144f),
            angle = 12f,
            marginWidth = 2.5f,
            trackWidth = 6f,
            crops = new List<HorizonCrop> { new() { layer = 3, weight = 1f }, new() { layer = 5, weight = 3f } },
        };

        [Test]
        public void Offsets_StartUnderTheTerrainAndEndAtTheWidth()
        {
            var offsets = HorizonRing.Offsets(Width);

            Assert.AreEqual(-HorizonRing.Tuck, offsets[0]);
            Assert.AreEqual(0f, offsets[1]);
            Assert.AreEqual(Width, offsets[^1]);
            for (var i = 2; i < offsets.Count - 1; i++)
            {
                Assert.Greater(offsets[i] - offsets[i - 1], offsets[i - 1] - offsets[i - 2] - 0.01f, "steps grow outward");
            }
        }

        [Test]
        public void Ring_ReachesUnderTheTerrainAndOutToItsWidth()
        {
            var mesh = HorizonRing.Build(Terrain, Width, _ => 0f, _ => 0f);
            try
            {
                var points = mesh.vertices.Select(v => new Vector2(v.x, v.z)).ToList();
                var distances = points.Select(p => HorizonRing.Distance(Terrain, p)).ToList();

                Assert.IsTrue(points.Any(p => Terrain.Contains(p)), "the first loop tucks under the terrain");
                Assert.AreEqual(Width, distances.Max(), 1f);
                Assert.IsTrue(mesh.normals.All(n => n.y > 0.99f), "flat ground faces up");
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void FieldAt_FindsTheOneFieldHoldingEachPointInsideTheCells()
        {
            var fields = Fields();
            var cells = fields.Cells(Area);

            for (var x = Area.xMin; x <= Area.xMax; x += 25f)
            {
                for (var y = Area.yMin; y <= Area.yMax; y += 25f)
                {
                    var grid = fields.ToGrid(new Vector2(x, y));
                    var field = fields.FieldAt(Seed, new Vector2(x, y), out var row);
                    var along = grid.x + row.slide;
                    var cell = new Vector2Int(Mathf.FloorToInt(along / fields.size.x), Mathf.FloorToInt(grid.y / fields.size.y));

                    Assert.IsTrue(cells.Contains(cell), $"({x}, {y}) is in cell {cell}");
                    Assert.IsTrue(row.start <= grid.y && grid.y < row.end, $"({x}, {y}) lies across its row");
                    Assert.IsTrue(field.start <= along && along < field.end, $"({x}, {y}) lies along its field");
                }
            }
        }

        [Test]
        public void Fields_TileEachRowInVariedSizesWithTracksOnlyWhereTheirNeighboursAgree()
        {
            var fields = Fields();
            var (lengths, widths) = (new HashSet<float>(), new HashSet<float>());
            for (var gridRow = -6; gridRow < 6; gridRow++)
            {
                for (var part = 0; part < 2; part++)
                {
                    var row = fields.Row(Seed, gridRow, part);
                    widths.Add(row.Width);
                    var line = RowFields(fields, gridRow, part, -6, 6);
                    for (var i = 1; i < line.Count; i++)
                    {
                        var where = $"row {gridRow}.{part}, field {i}";
                        Assert.AreEqual(line[i - 1].end, line[i].start, 0.001f, $"{where}: fields meet without gap or overlap");
                        Assert.AreEqual(line[i - 1].endMargin, line[i].startMargin, $"{where}: both sides of a field end agree");
                        var track = line[i].startMargin > fields.marginWidth;
                        var onCellEdge = Mathf.Approximately(Mathf.Repeat(line[i].start, fields.size.x), 0f);
                        Assert.IsTrue(!track || onCellEdge, $"{where}: a track runs only along a cell's end");
                    }

                    lengths.UnionWith(line.Select(field => Mathf.Round(field.Length)));
                }

                var south = fields.Row(Seed, gridRow, 0);
                var north = fields.Splits(Seed, gridRow) ? fields.Row(Seed, gridRow, 1) : fields.Row(Seed, gridRow + 1, 0);
                Assert.AreEqual(south.northMargin, north.southMargin, $"row {gridRow}: both sides of a row edge agree");
            }

            Assert.Greater(lengths.Count, 4, "fields vary in length");
            Assert.Greater(widths.Count(width => width > 0f), 4, "rows vary in width");
        }

        [Test]
        public void Belts_StandOnRowEdgesWithoutTracksPastTheClearance()
        {
            var fields = Fields();
            var belts = new HorizonBelts { share = 0.5f, strips = new List<Material> { null, null } };

            var stretches = belts.Stretches(fields, Seed, Terrain, Width);

            Assert.IsNotEmpty(stretches);
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, stretches.Select(stretch => stretch.strip).Distinct(), "both strips are picked");
            foreach (var stretch in stretches)
            {
                var (from, to) = (fields.ToGrid(stretch.from), fields.ToGrid(stretch.to));
                Assert.AreEqual(from.y, to.y, 0.01f, "a stretch runs along its row");
                for (var t = 0f; t <= 1f; t += 0.125f)
                {
                    var point = Vector2.Lerp(stretch.from, stretch.to, t);
                    var distance = HorizonRing.Distance(Terrain, point);
                    Assert.GreaterOrEqual(distance, belts.clearance - 0.01f, "no belt stands near the terrain");
                    Assert.LessOrEqual(distance, Width + 0.01f, "no belt stands past the ring");
                }

                var nudged = fields.ToWorld(new Vector2((from.x + to.x) * 0.5f, from.y + 0.5f));
                fields.FieldAt(Seed, nudged, out var row);
                Assert.AreEqual(from.y, row.start, 0.01f, "a stretch stands on a row's south edge");
                Assert.AreEqual(fields.marginWidth, row.southMargin, "no belt stands along a track");
            }
        }

        [Test]
        public void RaisedFields_StandOnlyOnCanopyCropsInsideTheirMarginsPastTheTerrain()
        {
            var fields = Fields();
            var crop = ScriptableObject.CreateInstance<RowCrop>();
            var canopy = new Material(Shader.Find("Hidden/InternalErrorShader"));
            crop.canopyMaterial = canopy;
            fields.crops[0] = new HorizonCrop { layer = 3, weight = 1f, crop = crop };
            var raised = new HorizonRaisedFields();
            try
            {
                var boxes = raised.Fields(fields, Seed, Terrain, Width);

                Assert.IsNotEmpty(boxes);
                foreach (var box in boxes)
                {
                    Assert.AreEqual(0, box.crop, "only the crop with a canopy stands raised");
                    var field = fields.FieldAt(Seed, fields.ToWorld(box.area.center), out var row);
                    var along = new Vector2(field.start + field.startMargin, field.end - field.endMargin) - Vector2.one * row.slide;
                    Assert.AreEqual(along.x, box.area.xMin, 0.01f, "the box starts inside its field's margin");
                    Assert.AreEqual(along.y, box.area.xMax, 0.01f, "the box ends inside its field's margin");
                    Assert.AreEqual(row.start + row.southMargin, box.area.yMin, 0.01f, "the box keeps off its row's south margin");
                    Assert.AreEqual(row.end - row.northMargin, box.area.yMax, 0.01f, "the box keeps off its row's north margin");
                    Assert.Greater(HorizonRing.Distance(Terrain, fields.ToWorld(box.area.min)), 0f, "no box stands on the terrain");
                }

                var mesh = raised.Build(boxes, fields, _ => 0f);
                Assert.AreEqual(crop.height, mesh.vertices.Max(vertex => vertex.y), 0.001f, "the tops stand at the crop's height");
                Object.DestroyImmediate(mesh);
            }
            finally
            {
                Object.DestroyImmediate(canopy);
                Object.DestroyImmediate(crop);
            }
        }

        [Test]
        public void Texture_ListsTheCropsAndEachCellsFields()
        {
            var fields = Fields();
            var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            var cells = fields.Cells(new Rect(-1000f, -1000f, 2000f, 2000f));
            var texture = fields.Apply(material, Seed, cells);
            try
            {
                Assert.AreEqual(new Color(3f, 0.25f, 0f, 0f), texture.GetPixel(0, 0));
                Assert.AreEqual(new Color(5f, 0.75f, 0f, 0f), texture.GetPixel(1, 0));
                var (column, gridRow) = (cells.xMin + 2, cells.yMin + 1);
                var field = fields.Field(Seed, gridRow, 1, column, 1);
                var cellStart = column * fields.size.x;
                var texelRow = 1 + 2 * (gridRow - cells.yMin) + 1;
                var texel = 2 + 4 * (column - cells.xMin) + 2;
                var expected = new Color(field.layer, field.shade, field.start - cellStart, field.end - cellStart);
                Assert.AreEqual(expected, texture.GetPixel(texel, texelRow));
                Assert.AreEqual(new Color(field.startMargin, field.endMargin, field.block, 0f), texture.GetPixel(texel + 1, texelRow));
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(material);
            }
        }

        // A row's distinct fields from cell first to cell last, in order along it.
        private static List<HorizonField> RowFields(HorizonFields fields, int gridRow, int part, int first, int last)
        {
            var line = new List<HorizonField>();
            for (var column = first; column < last; column++)
            {
                for (var cellPart = 0; cellPart < 2; cellPart++)
                {
                    var field = fields.Field(Seed, gridRow, part, column, cellPart);
                    if (line.Count == 0 || !Mathf.Approximately(line[^1].start, field.start))
                    {
                        line.Add(field);
                    }
                }
            }

            return line;
        }
    }
}
