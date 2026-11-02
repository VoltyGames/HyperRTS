using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Environment.Vegetation.Scatter;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Horizon
{
    /// <summary>
    /// Shelterbelts along some of the horizon's field edges: real trees near the terrain, where a tree still covers
    /// many pixels, and past that a tree line, one low mesh shaped like a belt's crowns.
    /// </summary>
    [Serializable]
    public sealed class HorizonBelts
    {
        private const uint BeltSalt = 0x42u;

        [Tooltip("Share of field edges that carry a belt.")]
        [Range(0f, 1f)]
        public float share = 0.35f;

        [Tooltip("Full width across a belt (metres).")]
        [Min(1f)]
        public float width = 8f;

        [Min(1)]
        public int rows = 2;

        [Tooltip("Distance between trees along a row (metres).")]
        [Min(1f)]
        public float spacing = 5f;

        [Tooltip("Metres past the terrain's edge within which belts are real trees; further out they are a tree line.")]
        [Min(0f)]
        public float treesWidth = 250f;

        [Tooltip("Tree variants the belts pick from by weight. Each needs an LOD Group, whose last LOD is drawn, and " +
                 "a HyperRTS/Foliage material, whose leaves colour the tree line.")]
        public List<HorizonTree> trees = new();

        /// <summary>
        /// The field edges inside <paramref name="area"/> that carry a belt, in world space, each with its own hash:
        /// every field's south and west edge, kept by <see cref="share"/>.
        /// </summary>
        public List<HorizonBelt> Belts(HorizonFields fields, int seed, Rect area)
        {
            var belts = new List<HorizonBelt>();
            var cells = fields.Cells(area);
            var salted = HorizonFields.Salted(seed, BeltSalt);
            for (var row = cells.yMin; row < cells.yMax; row++)
            {
                for (var column = cells.xMin; column < cells.xMax; column++)
                {
                    var start = fields.FieldStart(seed, column, row);
                    TryAdd(belts, fields, start, new Vector2(fields.size.x, 0f), ScatterHash.Hash(salted, column, row * 2));
                    TryAdd(belts, fields, start, new Vector2(0f, fields.size.y), ScatterHash.Hash(salted, column, row * 2 + 1));
                }
            }

            return belts;
        }

        private void TryAdd(List<HorizonBelt> belts, HorizonFields fields, Vector2 start, Vector2 run, uint hash)
        {
            if (Hashes.Fraction(hash) < share)
            {
                belts.Add(new HorizonBelt(fields.ToWorld(start), fields.ToWorld(start + run), hash));
            }
        }

        /// <summary>Adds what keeps the belts from growing.</summary>
        public void AddProblems(List<string> problems)
        {
            if (share > 0f && trees.Sum(tree => tree.weight) <= 0f)
            {
                problems.Add("belts without trees");
            }

            trees.ForEach(tree => tree.AddProblems(problems));
        }
    }

    /// <summary>One belt along a field edge, in world space, with the hash its trees and tree line draw from.</summary>
    public readonly struct HorizonBelt
    {
        public HorizonBelt(Vector2 from, Vector2 to, uint hash)
        {
            From = from;
            To = to;
            Hash = hash;
        }

        public Vector2 From { get; }

        public Vector2 To { get; }

        public uint Hash { get; }
    }

    /// <summary>One tree variant of the horizon's belts and how often it grows.</summary>
    [Serializable]
    public struct HorizonTree
    {
        private const string FoliageShader = "HyperRTS/Foliage";

        public GameObject prefab;

        [Min(0f)]
        public float weight;

        /// <summary>The prefab's last LOD, or null when it has no LOD Group or that LOD isn't one mesh renderer.</summary>
        public MeshRenderer LastLod()
        {
            var group = prefab.GetComponent<LODGroup>();
            if (group == null || group.lodCount == 0)
            {
                return null;
            }

            var renderers = group.GetLODs()[^1].renderers;
            return renderers.Length == 1 ? renderers[0] as MeshRenderer : null;
        }

        /// <summary>The last LOD's leaves, whose colour the tree line takes, or null.</summary>
        public Material Leaves() =>
            LastLod().sharedMaterials.FirstOrDefault(material => material != null && material.shader.name == FoliageShader);

        /// <summary>Adds what keeps the tree from growing in a belt.</summary>
        public void AddProblems(List<string> problems)
        {
            if (prefab == null)
            {
                problems.Add("a belt tree has no prefab");
            }
            else if (LastLod() == null)
            {
                problems.Add($"belt tree '{prefab.name}' needs an LOD Group whose last LOD is one mesh renderer");
            }
            else if (Leaves() == null)
            {
                problems.Add($"belt tree '{prefab.name}' has no {FoliageShader} leaves");
            }
        }
    }
}
