using System;
using HyperRTS.Core;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>A map's ground grid: one <see cref="SurfaceMaterial"/> byte per cell, row 0 south, written by the map build.</summary>
    [HelpURL(HyperRTSDocs.Effects)]
    public class GroundMaterialMap : ScriptableObject
    {
        [Tooltip("Cells across, west to east.")]
        public int width;

        [Tooltip("Cells deep, south to north.")]
        public int height;

        [Tooltip("Width of one cell (metres).")]
        public float spacing = 1f;

        [Tooltip("Row-major SurfaceMaterial bytes from the south-west corner.")]
        public byte[] cells = { };

        /// <summary>Throws naming the asset when the grid can't be baked.</summary>
        public void Require()
        {
            if (width <= 0 || height <= 0)
            {
                throw Problem($"has a {width} x {height} grid");
            }

            if (cells.Length != width * height)
            {
                throw Problem($"holds {cells.Length} cells, not {width} x {height}");
            }

            if (spacing <= 0f)
            {
                throw Problem($"needs a spacing above 0, not {spacing}");
            }
        }

        /// <summary>The singleton with this grid's south-west corner at <paramref name="min"/>.</summary>
        public GroundMaterials Bake(float2 min, Allocator allocator)
        {
            Require();
            using var stored = new NativeArray<byte>(cells, Allocator.Temp);
            return GroundMaterials.Create(stored, new int2(width, height), min, spacing, allocator);
        }

        private InvalidOperationException Problem(string problem) =>
            new($"HyperRTS: ground material map '{name}' {problem}.");
    }
}
