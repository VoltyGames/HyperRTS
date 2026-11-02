using System;
using System.Collections.Generic;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Environment.Vegetation.Scatter;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Horizon
{
    /// <summary>
    /// The horizon's farmland: rows of large fields turned to the map's farmland, each row slid by a random share of a
    /// field, each field one crop picked by weight, with a margin along its edges. HorizonFields.hlsl paints the same
    /// grid, so the belts along its edges line up with the ground.
    /// </summary>
    [Serializable]
    public sealed class HorizonFields
    {
        /// <summary>Crops HorizonGround.shader takes.</summary>
        public const int MaxCrops = 8;

        /// <summary>Salts the rows' slide; HorizonFields.hlsl slides them with the same.</summary>
        public const uint RowSalt = 0x52u;

        /// <summary>The largest seed a float carries exactly to the shader.</summary>
        public const int MaxSeed = (1 << 24) - 1;

        private static readonly int FieldGridId = Shader.PropertyToID("_FieldGrid");
        private static readonly int FieldMarginId = Shader.PropertyToID("_FieldMargin");
        private static readonly int CropLayersLowId = Shader.PropertyToID("_CropLayersLow");
        private static readonly int CropLayersHighId = Shader.PropertyToID("_CropLayersHigh");
        private static readonly int CropRollsLowId = Shader.PropertyToID("_CropRollsLow");
        private static readonly int CropRollsHighId = Shader.PropertyToID("_CropRollsHigh");

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

        [Tooltip("Crops the fields pick from by weight, at most eight.")]
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

        /// <summary>How far along x a row's fields are slid, under one field's length.</summary>
        public float RowShift(int seed, int row) => Hashes.Fraction(ScatterHash.Hash(Salted(seed, RowSalt), row, 0)) * size.x;

        /// <summary>The grid corner a field starts at: its west end on its south edge.</summary>
        public Vector2 FieldStart(int seed, int column, int row) =>
            new(column * size.x - RowShift(seed, row), row * size.y);

        /// <summary>Hands the grid and the crops to HorizonGround.shader; the seed travels as a float, so 24 bits.</summary>
        public void Apply(Material material, int seed)
        {
            if (crops.Count is 0 or > MaxCrops)
            {
                throw new InvalidOperationException($"HyperRTS: the horizon needs 1 to {MaxCrops} crops, not {crops.Count}.");
            }

            if (seed is < 0 or > MaxSeed)
            {
                throw new ArgumentOutOfRangeException(nameof(seed), seed, "HyperRTS: a horizon seed fits 24 bits.");
            }

            var radians = angle * Mathf.Deg2Rad;
            material.SetVector(FieldGridId, new Vector4(size.x, size.y, Mathf.Cos(radians), Mathf.Sin(radians)));
            material.SetVector(FieldMarginId, new Vector4(marginWidth, marginLayer, crops.Count, seed));
            var (layers, rolls) = (new float[MaxCrops], new float[MaxCrops]);
            var total = 0f;
            crops.ForEach(crop => total += crop.weight);
            var sum = 0f;
            for (var i = 0; i < MaxCrops; i++)
            {
                var crop = crops[Mathf.Min(i, crops.Count - 1)];
                sum += i < crops.Count ? crop.weight : 0f;
                layers[i] = crop.layer;
                rolls[i] = i < crops.Count - 1 ? sum / total : 1f;
            }

            material.SetVector(CropLayersLowId, new Vector4(layers[0], layers[1], layers[2], layers[3]));
            material.SetVector(CropLayersHighId, new Vector4(layers[4], layers[5], layers[6], layers[7]));
            material.SetVector(CropRollsLowId, new Vector4(rolls[0], rolls[1], rolls[2], rolls[3]));
            material.SetVector(CropRollsHighId, new Vector4(rolls[4], rolls[5], rolls[6], rolls[7]));
        }

        public static int Salted(int seed, uint salt) => unchecked((int)((uint)seed ^ salt));
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
