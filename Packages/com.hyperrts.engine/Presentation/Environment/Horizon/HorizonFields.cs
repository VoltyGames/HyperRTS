using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Environment.Vegetation.Scatter;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Horizon
{
    /// <summary>
    /// The horizon's farmland as a grid of fields of varied size with dirt tracks, all hashed from the seed.
    /// </summary>
    [Serializable]
    public sealed class HorizonFields
    {
        private const uint SlideSalt = 0x52u;
        private const uint CropSalt = 0x43u;
        private const uint ShadeSalt = 0x53u;
        private const uint RowSplitSalt = 0x41u;
        private const uint CellSplitSalt = 0x4Cu;
        private const uint MergeSalt = 0x4Du;
        private const uint EndTrackSalt = 0x45u;
        private const uint EdgeTrackSalt = 0x54u;
        private const uint BlockSalt = 0x42u;

        // Splits fall between these shares of a cell, so neither part is a sliver.
        private const float SplitMin = 0.3f;
        private const float SplitMax = 0.7f;

        private const int BlockCells = 3;

        private static readonly int FieldGridId = Shader.PropertyToID("_FieldGrid");
        private static readonly int FieldMarginId = Shader.PropertyToID("_FieldMargin");
        private static readonly int FieldCellsId = Shader.PropertyToID("_FieldCells");
        private static readonly int FieldsId = Shader.PropertyToID("_Fields");

        [Tooltip("Cell length along the rows (x) and width across them (y), in metres: the kit's field size.")]
        public Vector2 size = new(256f, 144f);

        [Tooltip("Degrees the grid is turned, as the map's farmland.")]
        public float angle;

        [Tooltip("Weedy strip along each field edge (metres).")]
        [Min(0f)]
        public float marginWidth = 5f;

        [Tooltip("Terrain layer of the margins and tracks, in the map terrain material's layer arrays.")]
        [Min(0)]
        public int marginLayer;

        [Tooltip("Share of grid rows split in two across, and of cells split in two along.")]
        [Range(0f, 1f)]
        public float splitShare = 0.4f;

        [Tooltip("Share of neighbouring cell pairs merged into one long field.")]
        [Range(0f, 1f)]
        public float mergeShare = 0.25f;

        [Tooltip("Share of field ends and row edges a dirt track runs along.")]
        [Range(0f, 1f)]
        public float trackShare = 0.3f;

        [Tooltip("Full width of a dirt track (metres).")]
        [Min(0f)]
        public float trackWidth = 6f;

        [Tooltip("Crops the fields pick from by weight.")]
        public List<HorizonCrop> crops = new();

        /// <summary>A world point turned into the grid, x along the rows.</summary>
        public Vector2 ToGrid(Vector2 world)
        {
            var (cos, sin) = (Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            return new Vector2(world.x * cos + world.y * sin, -world.x * sin + world.y * cos);
        }

        /// <summary>A grid point turned back into the world.</summary>
        public Vector2 ToWorld(Vector2 grid)
        {
            var (cos, sin) = (Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            return new Vector2(grid.x * cos - grid.y * sin, grid.x * sin + grid.y * cos);
        }

        /// <summary>The cells holding <paramref name="area"/> whatever each row's slide: columns along x, rows along y.</summary>
        public RectInt Cells(Rect area)
        {
            var corners = new[]
            {
                ToGrid(area.min), ToGrid(area.max), ToGrid(new Vector2(area.xMin, area.yMax)), ToGrid(new Vector2(area.xMax, area.yMin)),
            };
            var (min, max) = (corners[0], corners[0]);
            foreach (var corner in corners)
            {
                (min, max) = (Vector2.Min(min, corner), Vector2.Max(max, corner));
            }

            // A row's slide moves its columns up to one cell further along x.
            var first = Vector2Int.FloorToInt(min / size);
            var last = Vector2Int.FloorToInt(max / size) + Vector2Int.right;
            return new RectInt(first, last - first + Vector2Int.one);
        }

        /// <summary>Whether grid row <paramref name="row"/> is split in two across.</summary>
        public bool Splits(int seed, int row) => Fraction(seed, RowSplitSalt, row, 0) < splitShare;

        /// <summary>Whether a track runs along the south edge of a row: part 0 at its grid row's start, part 1 at the split.</summary>
        public bool EdgeTrack(int seed, int row, int part) => Fraction(seed, EdgeTrackSalt, row, part) < trackShare;

        /// <summary>Whether a track runs across a row where cell <paramref name="column"/> starts, if a field ends there.</summary>
        public bool EndTrack(int seed, int row, int part, int column) =>
            Fraction(seed, EndTrackSalt, column, Line(row, part)) < trackShare;

        /// <summary>Part 0 of a grid row, or part 1 north of its split; an unsplit row's part 1 is empty.</summary>
        public HorizonRow Row(int seed, int row, int part)
        {
            var split = Splits(seed, row);
            var middle = split ? Mathf.Lerp(SplitMin, SplitMax, Fraction(seed, RowSplitSalt, row, 1)) * size.y : size.y;
            var bottom = row * size.y;
            var north = split && part == 0 ? EdgeMargin(seed, row, 1) : EdgeMargin(seed, row + 1, 0);
            return new HorizonRow
            {
                slide = Fraction(seed, SlideSalt, row, part) * size.x,
                start = bottom + (part == 0 ? 0f : middle),
                end = bottom + (part == 0 ? middle : size.y),
                southMargin = EdgeMargin(seed, row, part),
                northMargin = north,
            };
        }

        /// <summary>
        /// Part 0 or 1 of cell <paramref name="column"/> in a row: its two halves when the cell is split along, else
        /// the cell's one field both times, which spans its pair when merged.
        /// </summary>
        public HorizonField Field(int seed, int row, int part, int column, int cellPart)
        {
            var line = Line(row, part);
            var pair = Mathf.FloorToInt(column / 2f);
            if (Fraction(seed, MergeSalt, pair, line) < mergeShare)
            {
                var first = pair * 2;
                return CreateField(seed, row, line, first, 0, first * size.x, (first + 2) * size.x,
                    EndMargin(seed, row, part, first), EndMargin(seed, row, part, first + 2));
            }

            var (cellStart, cellEnd) = (column * size.x, (column + 1) * size.x);
            var (startMargin, endMargin) = (EndMargin(seed, row, part, column), EndMargin(seed, row, part, column + 1));
            if (Fraction(seed, CellSplitSalt, column, line) >= splitShare)
            {
                return CreateField(seed, row, line, column, 0, cellStart, cellEnd, startMargin, endMargin);
            }

            var middle = cellStart + Mathf.Lerp(SplitMin, SplitMax, Fraction(seed, CellSplitSalt, column, line, 1)) * size.x;
            return cellPart == 0
                ? CreateField(seed, row, line, column, 0, cellStart, middle, startMargin, marginWidth)
                : CreateField(seed, row, line, column, 1, middle, cellEnd, marginWidth, endMargin);
        }

        /// <summary>The field holding a world point, as HorizonGround.shader finds it, and its row.</summary>
        public HorizonField FieldAt(int seed, Vector2 world, out HorizonRow row)
        {
            var grid = ToGrid(world);
            var gridRow = Mathf.FloorToInt(grid.y / size.y);
            var part = grid.y >= Row(seed, gridRow, 0).end ? 1 : 0;
            row = Row(seed, gridRow, part);
            var along = grid.x + row.slide;
            var column = Mathf.FloorToInt(along / size.x);
            var field = Field(seed, gridRow, part, column, 0);
            return along < field.end ? field : Field(seed, gridRow, part, column, 1);
        }

        /// <summary>
        /// Hands HorizonGround.shader the grid and a texture of <paramref name="cells"/>, which the caller destroys.
        /// </summary>
        public Texture2D Apply(Material material, int seed, RectInt cells)
        {
            var texture = new Texture2D(Mathf.Max(2 + 4 * cells.width, crops.Count), 1 + 2 * cells.height, TextureFormat.RGBAFloat,
                false, true)
            {
                name = "Horizon Fields",
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var total = crops.Sum(crop => crop.weight);
            for (var i = 0; i < crops.Count; i++)
            {
                texture.SetPixel(i, 0, new Color(crops[i].layer, crops[i].weight / total, 0f, 0f));
            }

            var (marginArea, area) = (0f, 0f);
            for (var row = cells.yMin; row < cells.yMax; row++)
            {
                for (var part = 0; part < 2; part++)
                {
                    var (rowMargins, rowArea) = WriteRow(texture, seed, cells, row, part);
                    (marginArea, area) = (marginArea + rowMargins, area + rowArea);
                }
            }

            texture.Apply(false);
            var radians = angle * Mathf.Deg2Rad;
            material.SetVector(FieldGridId, new Vector4(size.x, size.y, Mathf.Cos(radians), Mathf.Sin(radians)));
            material.SetVector(FieldMarginId, new Vector4(marginLayer, marginArea / area, 0f, 0f));
            material.SetVector(FieldCellsId, new Vector4(cells.xMin, cells.yMin, crops.Count, 0f));
            material.SetTexture(FieldsId, texture);
            return texture;
        }

        // One row part's texels; returns its margin area and area, a merged field counting half in each of its cells.
        private (float margins, float area) WriteRow(Texture2D texture, int seed, RectInt cells, int row, int part)
        {
            var texelRow = 1 + 2 * (row - cells.yMin) + part;
            var cellRow = Row(seed, row, part);
            var bottom = row * size.y;
            texture.SetPixel(0, texelRow, new Color(cellRow.slide, cellRow.start - bottom, cellRow.end - bottom, 0f));
            texture.SetPixel(1, texelRow, new Color(cellRow.southMargin, cellRow.northMargin, 0f, 0f));
            var (margins, area) = (0f, 0f);
            for (var column = cells.xMin; column < cells.xMax; column++)
            {
                var (cellStart, cellEnd) = (column * size.x, (column + 1) * size.x);
                for (var cellPart = 0; cellPart < 2; cellPart++)
                {
                    var field = Field(seed, row, part, column, cellPart);
                    var texel = 2 + 4 * (column - cells.xMin) + 2 * cellPart;
                    texture.SetPixel(texel, texelRow, new Color(field.layer, field.shade, field.start - cellStart, field.end - cellStart));
                    texture.SetPixel(texel + 1, texelRow, new Color(field.startMargin, field.endMargin, field.block, 0f));
                    var unsplit = cellPart == 1 && field.start == Field(seed, row, part, column, 0).start;
                    if (unsplit)
                    {
                        continue;
                    }

                    var inCell = Mathf.Min(field.end, cellEnd) - Mathf.Max(field.start, cellStart);
                    margins += field.MarginArea(cellRow) * inCell / field.Length;
                    area += inCell * cellRow.Width;
                }
            }

            return (margins, area);
        }

        // Each row part has its own line of cells, keyed apart.
        private static int Line(int row, int part) => row * 2 + part;

        private float EdgeMargin(int seed, int row, int part) => EdgeTrack(seed, row, part) ? trackWidth * 0.5f : marginWidth;

        private float EndMargin(int seed, int row, int part, int column) =>
            EndTrack(seed, row, part, column) ? trackWidth * 0.5f : marginWidth;

        private HorizonField CreateField(int seed, int row, int line, int column, int cellPart, float start, float end,
            float startMargin, float endMargin)
        {
            var weights = crops.Select(crop => crop.weight).ToArray();
            var crop = Hashes.Pick(ScatterHash.Channel(Hash(seed, CropSalt, column, line), (uint)cellPart), weights);
            return new HorizonField
            {
                crop = crop,
                layer = crops[crop].layer,
                shade = Hashes.Fraction(ScatterHash.Channel(Hash(seed, ShadeSalt, column, line), (uint)cellPart)),
                block = Fraction(seed, BlockSalt, Mathf.FloorToInt(column / (float)BlockCells), Mathf.FloorToInt(row / (float)BlockCells)),
                start = start,
                end = end,
                startMargin = startMargin,
                endMargin = endMargin,
            };
        }

        private static uint Hash(int seed, uint salt, int x, int y) => ScatterHash.Hash(ScatterHash.Salted(seed, salt), x, y);

        private static float Fraction(int seed, uint salt, int x, int y, uint channel = 0u) =>
            Hashes.Fraction(ScatterHash.Channel(Hash(seed, salt, x, y), channel));
    }
}
