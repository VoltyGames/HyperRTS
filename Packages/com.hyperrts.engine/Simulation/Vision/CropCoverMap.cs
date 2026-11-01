using System;
using HyperRTS.Core;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>
    /// A map's crop cover written by the map build: per cell, the share of sight its crop takes per metre and the
    /// crop's top, in <see cref="SightBlockers"/> steps. Row 0 is south.
    /// </summary>
    [HelpURL(HyperRTSDocs.Vision)]
    public class CropCoverMap : ScriptableObject
    {
        [Tooltip("Cells across, west to east.")]
        public int width;

        [Tooltip("Cells deep, south to north.")]
        public int height;

        [Tooltip("Width of one cell (metres).")]
        public float spacing = 1f;

        [Tooltip("Row-major sight lost per metre, in 1/250 steps, from the south-west corner.")]
        public byte[] density = { };

        [Tooltip("Row-major crop top above the ground, in 0.25 m steps.")]
        public byte[] top = { };

        /// <summary>Throws naming the asset when the grid can't be baked.</summary>
        public void Require()
        {
            if (width <= 0 || height <= 0 || spacing <= 0f)
            {
                throw Problem($"has a {width} x {height} grid of {spacing} m cells");
            }

            if (density.Length != width * height || top.Length != density.Length)
            {
                throw Problem($"holds {density.Length} densities and {top.Length} tops, not {width} x {height}");
            }
        }

        /// <summary>The singleton with this grid's south-west corner at <paramref name="min"/>.</summary>
        public CropCover Bake(float2 min, Allocator allocator)
        {
            Require();
            using var densities = new NativeArray<byte>(density, Allocator.Temp);
            using var tops = new NativeArray<byte>(top, Allocator.Temp);
            return CropCover.Create(densities, tops, new int2(width, height), min, spacing, allocator);
        }

        private InvalidOperationException Problem(string problem) =>
            new($"HyperRTS: crop cover map '{name}' {problem}.");
    }
}
