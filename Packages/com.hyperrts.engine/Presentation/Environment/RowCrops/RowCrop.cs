using System.Collections.Generic;
using HyperRTS.Core;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.RowCrops
{
    /// <summary>A crop sown in rows that <see cref="RowCropRenderer"/> grows on the GPU: its meshes, materials and sowing.</summary>
    [Icon(HyperRTSIcons.Environment)]
    [HelpURL(HyperRTSDocs.Presentation)]
    public sealed class RowCrop : ScriptableObject
    {
        [Header("Meshes")]
        [Tooltip("Full plant mesh, origin at the root, one submesh.")]
        public Mesh lod0;

        [Tooltip("Simpler plant mesh past lod0Distance, one submesh.")]
        public Mesh lod1;

        [Tooltip("Impostor card past lod1Distance, one submesh.")]
        public Mesh card;

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

        [Tooltip("Bare margin in metres round field edges, roads and placed objects.")]
        public float edgeMargin = 0.6f;

        [Tooltip("Tallest plant height in metres, for culling.")]
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
            Single(problems, lod0, nameof(lod0));
            Single(problems, lod1, nameof(lod1));
            Single(problems, card, nameof(card));
            Require(problems, material != null, $"{nameof(material)} is missing");
            Require(problems, cardMaterial != null, $"{nameof(cardMaterial)} is missing");
            Require(problems, rowSpacing > 0f, $"{nameof(rowSpacing)} {rowSpacing} must be above 0");
            Require(problems, plantSpacing > 0f, $"{nameof(plantSpacing)} {plantSpacing} must be above 0");
            Require(problems, jitter is >= 0f and <= 0.5f, $"{nameof(jitter)} {jitter} must be 0 to 0.5");
            Require(problems, scale.x > 0f && scale.y >= scale.x, $"{nameof(scale)} {scale} must be above 0 with y at least x");
            Require(problems, edgeMargin >= 0f, $"{nameof(edgeMargin)} {edgeMargin} must not be negative");
            Require(problems, height > 0f, $"{nameof(height)} {height} must be above 0");
            Require(problems, lod0Distance > 0f, $"{nameof(lod0Distance)} {lod0Distance} must be above 0");
            Require(problems, lod1Distance > lod0Distance,
                $"{nameof(lod1Distance)} {lod1Distance} must be above {nameof(lod0Distance)} {lod0Distance}");
            return problems;
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
