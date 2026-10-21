using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Grass
{
    /// <summary>One plant <see cref="GrassRenderer"/> scatters: its models, material, size, tints and density.</summary>
    [Serializable]
    public sealed class GrassPlant
    {
        [Tooltip("The plant's key, named in errors.")]
        public string name;

        [Tooltip("The plant's models, origin at the root, one submesh each; every plant drawn picks one from its hash.")]
        public List<Mesh> meshes = new();

        [Tooltip("HyperRTS/Foliage material the models share.")]
        public Material material;

        [Tooltip("Random horizontal scale range of the models.")]
        public Vector2 width = Vector2.one;

        [Tooltip("Random vertical scale range of the models.")]
        public Vector2 height = Vector2.one;

        [Tooltip("Tint of plants in green patches.")]
        public Color healthy = Color.white;

        [Tooltip("Tint of plants in dry patches.")]
        public Color dry = Color.white;

        [Tooltip("Plants per square metre at full coverage.")]
        public float density = 1f;

        [Tooltip("How strongly the plants gather into clumps and tufts: 0 spreads them evenly.")]
        [Range(0f, 1f)]
        public float clumping = 0.5f;

        /// <summary>Metres between the plant's lattice cells: one plant per cell at full coverage.</summary>
        public float Spacing => 1f / Mathf.Sqrt(density);

        /// <summary>The tallest the plant grows, before the far plants' growth.</summary>
        public float Tallest
        {
            get
            {
                var top = 0f;
                meshes.ForEach(mesh => top = Mathf.Max(top, mesh.bounds.max.y));
                return top * height.y;
            }
        }

        /// <summary>What Grass.compute reads of the plant, the <paramref name="index"/>th of its field.</summary>
        public GrassPlantGpu ToGpu(int index, int firstSlot) => new()
        {
            spacing = Spacing,
            clumping = clumping,
            width = width,
            height = height,
            variants = meshes.Count,
            firstSlot = firstSlot,
            seed = (index + 1) * 0x2545F491,
        };

        /// <summary>Problems that keep the plant from drawing, one per line. Empty when ready.</summary>
        public IReadOnlyList<string> Problems()
        {
            var problems = new List<string>();
            Meshes(problems);
            Require(problems, material != null, $"{nameof(material)} is missing");
            Require(problems, width.x > 0f && width.y >= width.x, $"{nameof(width)} {width} must be above 0 with y at least x");
            Require(problems, height.x > 0f && height.y >= height.x,
                $"{nameof(height)} {height} must be above 0 with y at least x");
            Require(problems, density > 0f, $"{nameof(density)} {density} must be above 0");
            Require(problems, clumping is >= 0f and <= 1f, $"{nameof(clumping)} {clumping} must be 0 to 1");
            return problems;
        }

        private void Meshes(List<string> problems)
        {
            if (meshes.Count == 0)
            {
                problems.Add($"{nameof(meshes)} is empty");
                return;
            }

            for (var i = 0; i < meshes.Count; i++)
            {
                Require(problems, meshes[i] != null, $"{nameof(meshes)}[{i}] is missing");
            }
        }

        private static void Require(List<string> problems, bool met, string problem)
        {
            if (!met)
            {
                problems.Add(problem);
            }
        }
    }
}
