using System.Collections.Generic;
using HyperRTS.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace HyperRTS.Presentation.Environment.RowCrops
{
    /// <summary>A crop sown in rows that <see cref="RowCropRenderer"/> grows on the GPU: its models, materials and sowing.</summary>
    [Icon(HyperRTSIcons.Environment)]
    [HelpURL(HyperRTSDocs.Presentation)]
    public sealed class RowCrop : ScriptableObject
    {
        [Header("Meshes")]
        [Tooltip("The crop's models; each plant draws one, picked from its hash.")]
        public List<RowCropVariant> variants = new();

        [Tooltip("HyperRTS/RowCrop material of both plant meshes.")]
        public Material material;

        [Tooltip("HyperRTS/RowCrop material of the card.")]
        public Material cardMaterial;

        [Header("Sowing")]
        [Tooltip("Metres between rows.")]
        public float rowSpacing = 0.75f;

        [Tooltip("Metres between plants along a row.")]
        public float plantSpacing = 0.3f;

        [Tooltip("Share of the spacing a plant strays from its row spot.")]
        [Range(0f, 0.5f)]
        public float jitter = 0.15f;

        [Tooltip("Random uniform scale range.")]
        public Vector2 scale = new(0.9f, 1.1f);

        [Tooltip("Plants face along their row. Off keeps the model's heading (+Z north), for crops that turn to the sun.")]
        public bool facesRow = true;

        [Tooltip("Degrees a plant turns either way from its heading.")]
        [Range(0f, 180f)]
        public float yawJitter = 20f;

        [Tooltip("Degrees a plant leans, in a random direction.")]
        [Range(0f, 30f)]
        public float lean = 3f;

        [Tooltip("Degrees a plant leans at the field edge.")]
        [Range(0f, 30f)]
        public float edgeLean = 9f;

        [Tooltip("Metres rows wander across their line, together and slowly.")]
        [Range(0f, 0.5f)]
        public float wander = 0.05f;

        [Tooltip("Metres over which a row's wander turns.")]
        public float wanderLength = 6f;

        [Tooltip("Scale multiplier range over the field's taller and shorter stretches, on top of each plant's scale.")]
        [FormerlySerializedAs("patchScale")]
        public Vector2 growthVariation = new(0.9f, 1.1f);

        [Tooltip("Metres across a stretch of taller or shorter plants.")]
        [FormerlySerializedAs("patchSize")]
        public float growthVariationSize = 12f;

        [Tooltip("Bare margin in metres round field edges, roads and placed objects.")]
        public float edgeMargin = 0.6f;

        [Tooltip("Tallest plant height in metres, for culling and the canopy's shade.")]
        public float height = 2f;

        [Header("Distances")]
        [Tooltip("Metres from the camera the full mesh is drawn to.")]
        public float lod0Distance = 40f;

        [Tooltip("Metres from the camera the simple mesh is drawn to. Cards draw past it.")]
        public float lod1Distance = 120f;

        [Tooltip("Whether the far cards cast shadows.")]
        public bool cardShadows;

        /// <summary>Square metres each plant takes.</summary>
        public float PlantArea => rowSpacing * plantSpacing;

        /// <summary>Problems that keep the crop from drawing, one per line. Empty when ready.</summary>
        public IReadOnlyList<string> Problems()
        {
            var problems = new List<string>();
            Variants(problems);
            Require(problems, material != null, $"{nameof(material)} is missing");
            Require(problems, cardMaterial != null, $"{nameof(cardMaterial)} is missing");
            Require(problems, rowSpacing > 0f, $"{nameof(rowSpacing)} {rowSpacing} must be above 0");
            Require(problems, plantSpacing > 0f, $"{nameof(plantSpacing)} {plantSpacing} must be above 0");
            Require(problems, jitter is >= 0f and <= 0.5f, $"{nameof(jitter)} {jitter} must be 0 to 0.5");
            Require(problems, scale.x > 0f && scale.y >= scale.x, $"{nameof(scale)} {scale} must be above 0 with y at least x");
            Require(problems, lean is >= 0f and <= 30f, $"{nameof(lean)} {lean} must be 0 to 30");
            Require(problems, edgeLean >= lean && edgeLean <= 30f,
                $"{nameof(edgeLean)} {edgeLean} must be {nameof(lean)} {lean} to 30");
            Require(problems, wander is >= 0f and <= 0.5f, $"{nameof(wander)} {wander} must be 0 to 0.5");
            Require(problems, wanderLength > 0f, $"{nameof(wanderLength)} {wanderLength} must be above 0");
            Require(problems, growthVariation.x > 0f && growthVariation.y >= growthVariation.x,
                $"{nameof(growthVariation)} {growthVariation} must be above 0 with y at least x");
            Require(problems, growthVariationSize > 0f,
                $"{nameof(growthVariationSize)} {growthVariationSize} must be above 0");
            Require(problems, edgeMargin >= 0f, $"{nameof(edgeMargin)} {edgeMargin} must not be negative");
            Require(problems, height > 0f, $"{nameof(height)} {height} must be above 0");
            Require(problems, lod0Distance > 0f, $"{nameof(lod0Distance)} {lod0Distance} must be above 0");
            Require(problems, lod1Distance > lod0Distance,
                $"{nameof(lod1Distance)} {lod1Distance} must be above {nameof(lod0Distance)} {lod0Distance}");
            return problems;
        }

        private void Variants(List<string> problems)
        {
            if (variants == null || variants.Count == 0)
            {
                problems.Add($"{nameof(variants)} is empty");
                return;
            }

            for (var v = 0; v < variants.Count; v++)
            {
                var variant = variants[v];
                if (variant == null)
                {
                    problems.Add($"{nameof(variants)}[{v}] is missing");
                    continue;
                }

                Single(problems, variant.lod0, $"{nameof(variants)}[{v}].{nameof(variant.lod0)}");
                Single(problems, variant.lod1, $"{nameof(variants)}[{v}].{nameof(variant.lod1)}");
                Single(problems, variant.card, $"{nameof(variants)}[{v}].{nameof(variant.card)}");
            }
        }

        private static void Single(List<string> problems, Mesh mesh, string field)
        {
            if (mesh == null)
            {
                problems.Add($"{field} is missing");
                return;
            }

            Require(problems, mesh.subMeshCount == 1, $"{field} '{mesh.name}' has {mesh.subMeshCount} submeshes; it needs one");
        }

        private static void Require(List<string> problems, bool holds, string problem)
        {
            if (!holds)
            {
                problems.Add(problem);
            }
        }
    }
}
