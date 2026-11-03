using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Environment.Vegetation.Scatter;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Horizon
{
    /// <summary>
    /// The horizon's farmland: rows of large fields turned to the map's farmland, each row slid by a random share of a
    /// field, each field one crop picked by weight, with a margin along its edges. HorizonGround.shader paints them from
    /// a texture laid out here.
    /// </summary>
    [Serializable]
    public sealed class HorizonFields
    {
        private const uint RowSalt = 0x52u;
        private const uint CropSalt = 0x43u;
        private const uint ShadeSalt = 0x53u;

        private static readonly int FieldGridId = Shader.PropertyToID("_FieldGrid");
        private static readonly int FieldMarginId = Shader.PropertyToID("_FieldMargin");
        private static readonly int FieldCellsId = Shader.PropertyToID("_FieldCells");
        private static readonly int FieldsId = Shader.PropertyToID("_Fields");

        [Tooltip("Field length along the rows (x) and width across them (y), in metres.")]
        public Vector2 size = new(256f, 144f);

        [Tooltip("Degrees the grid is turned, as the map's farmland.")]
        public float angle;

        [Tooltip("Weedy strip along each field edge (metres).")]
        [Min(0f)]
        public float marginWidth = 5f;

        [Tooltip("Terrain layer of the margins, in the map terrain material's layer arrays.")]
        [Min(0)]
        public int marginLayer;

        [Tooltip("Crops the fields pick from by weight.")]
        public List<HorizonCrop> crops = new();

        /// <summary>A world point turned into the grid, x along the rows.</summary>
        public Vector2 ToGrid(Vector2 world)
        {
            var (cos, sin) = (Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            return new Vector2(world.x * cos + world.y * sin, -world.x * sin + world.y * cos);
        }

        /// <summary>How far along x a row's fields are slid, under one field's length.</summary>
        public float RowShift(int seed, int row) => Hashes.Fraction(ScatterHash.Hash(ScatterHash.Salted(seed, RowSalt), row, 0)) * size.x;

        /// <summary>The fields holding <paramref name="area"/> whatever each row's slide: columns along x, rows along y.</summary>
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

            // A row's slide moves its columns up to one field further along x.
            var first = Vector2Int.FloorToInt(min / size);
            var last = Vector2Int.FloorToInt(max / size) + Vector2Int.right;
            return new RectInt(first, last - first + Vector2Int.one);
        }

        /// <summary>
        /// Hands HorizonGround.shader the grid and a texture of <paramref name="cells"/>, which the caller destroys.
        /// Row 0 lists the crops, each its layer and share of the weights; each row above is a row of fields, each
        /// texel its crop's layer, its shade and its row's slide.
        /// </summary>
        public Texture2D Apply(Material material, int seed, RectInt cells)
        {
            var texture = new Texture2D(Mathf.Max(cells.width, crops.Count), cells.height + 1, TextureFormat.RGBAFloat, false, true)
            {
                name = "Horizon Fields",
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var weights = crops.Select(crop => crop.weight).ToArray();
            var total = weights.Sum();
            for (var i = 0; i < crops.Count; i++)
            {
                texture.SetPixel(i, 0, new Color(crops[i].layer, weights[i] / total, 0f, 0f));
            }

            var (cropSeed, shadeSeed) = (ScatterHash.Salted(seed, CropSalt), ScatterHash.Salted(seed, ShadeSalt));
            for (var row = cells.yMin; row < cells.yMax; row++)
            {
                var shift = RowShift(seed, row);
                for (var column = cells.xMin; column < cells.xMax; column++)
                {
                    var crop = crops[Hashes.Pick(ScatterHash.Hash(cropSeed, column, row), weights)];
                    var shade = Hashes.Fraction(ScatterHash.Hash(shadeSeed, column, row));
                    texture.SetPixel(column - cells.xMin, row - cells.yMin + 1, new Color(crop.layer, shade, shift, 0f));
                }
            }

            texture.Apply(false);
            var radians = angle * Mathf.Deg2Rad;
            material.SetVector(FieldGridId, new Vector4(size.x, size.y, Mathf.Cos(radians), Mathf.Sin(radians)));
            material.SetVector(FieldMarginId, new Vector4(marginWidth, marginLayer, 0f, 0f));
            material.SetVector(FieldCellsId, new Vector4(cells.xMin, cells.yMin, crops.Count, 0f));
            material.SetTexture(FieldsId, texture);
            return texture;
        }
    }

    /// <summary>One crop of the horizon's fields: its terrain layer and how often a field grows it.</summary>
    [Serializable]
    public struct HorizonCrop
    {
        [Tooltip("Terrain layer in the map terrain material's layer arrays.")]
        [Min(0)]
        public int layer;

        [Min(0f)]
        public float weight;
    }
}
