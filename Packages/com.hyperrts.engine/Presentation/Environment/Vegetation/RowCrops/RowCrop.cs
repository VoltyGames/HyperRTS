using System.Collections.Generic;
using HyperRTS.Core;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Vegetation.RowCrops
{
    /// <summary>A crop sown in rows that <see cref="RowCropRenderer"/> grows on the GPU: its models, materials and planting.</summary>
    [Icon(HyperRTSIcons.Environment)]
    [HelpURL(HyperRTSDocs.Presentation)]
    public sealed class RowCrop : ScriptableObject
    {
        /// <summary>Extra lengths of canopyFade the hand-off reaches nearer at the flattest view (RowCropField.hlsl's HANDOVER_WIDEN).</summary>
        public const float GrazingWiden = 1.5f;

        /// <summary>Metres of field lattice that hold at most one lodged patch (RowCropField.hlsl's LODGING_CELL).</summary>
        public const float LodgingCell = 30f;

        [Header("Meshes")]
        [Tooltip("The crop's models; each plant draws one, picked from its hash.")]
        public List<RowCropVariant> variants = new();

        [Tooltip("HyperRTS/RowCrop material of both plant meshes.")]
        public Material material;

        [Tooltip("HyperRTS/RowCropCanopy material of the raised field surface drawn past the plants. Empty for a crop drawn by its plants alone, which leave over canopyFade before lod1Distance, as stubble does where the terrain carries it at range.")]
        public Material canopyMaterial;

        [Header("Planting")]
        [Tooltip("Metres between rows.")]
        public float rowSpacing = 0.75f;

        [Tooltip("Metres between plants along a row.")]
        public float plantSpacing = 0.3f;

        [Tooltip("Share of sown spots that came up; the rest leave gaps in the rows.")]
        [Range(0.5f, 1f)]
        public float stand = 1f;

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
        public Vector2 growthVariation = new(0.9f, 1.1f);

        [Tooltip("Metres across a stretch of taller or shorter plants.")]
        public float growthVariationSize = 12f;

        [Tooltip("Scale of plants at the field's edge, where they grow thinner and shorter.")]
        [Range(0.3f, 1f)]
        public float edgeGrowth = 1f;

        [Tooltip("Bare margin in metres round field edges, roads and placed objects.")]
        public float edgeMargin = 0.6f;

        [Tooltip("Share of the field's 30 m stretches holding a patch flattened by wind and rain, where plants lie over one way and the canopy drops. 0 for none.")]
        [Range(0f, 1f)]
        public float lodging;

        [Tooltip("Smallest and largest lodged patch across (metres).")]
        public Vector2 lodgedSize = new(3f, 15f);

        [Tooltip("Tallest plant height in metres, for culling and the canopy's shade.")]
        public float height = 2f;

        [Header("Distances")]
        [Tooltip("Metres from the camera the full mesh is drawn to.")]
        public float lod0Distance = 40f;

        [Tooltip("Metres from the camera the simple mesh is drawn to. The canopy draws past it.")]
        public float lod1Distance = 120f;

        [Tooltip("Metres from the camera plants start to thin out, from every plant kept to the far share at the simple mesh's far edge.")]
        public float thinStart = 10f;

        [Tooltip("Share of plants kept at the simple mesh's far edge. The share falls from 1 at the thinning start to this and holds, so far fields cost less.")]
        [Range(0.05f, 1f)]
        public float farShare = 1f;

        [Tooltip("Scale of plants at the simple mesh's far edge, so the thinner far plants still cover the ground. Grows from 1 as the share falls.")]
        [Range(1f, 2f)]
        public float farScale = 1f;

        [Header("Canopy")]
        [Tooltip("Rows across the canopy texture's tile.")]
        [Min(1)]
        public int canopyRows = 12;

        [Tooltip("Plants along a row of the canopy texture's tile.")]
        [Min(1)]
        public int canopyPlants = 28;

        [Tooltip("Metres before the simple mesh's far edge over which the plants leave and the canopy rises to its height.")]
        public float canopyFade = 10f;

        /// <summary>Square metres each plant takes.</summary>
        public float PlantArea => rowSpacing * plantSpacing;

        /// <summary>Whether the crop has a canopy past its plants; without one its plants just leave.</summary>
        public bool HasCanopy => canopyMaterial != null;

        /// <summary>Metres the canopy texture's tile spans: x across the rows, y along them.</summary>
        public Vector2 CanopyTile => new(canopyRows * rowSpacing, canopyPlants * plantSpacing);

        /// <summary>Growth variation as the kernel and the canopy read it: min and max scale, 1 / stretch size, edge scale.</summary>
        public Vector4 Growth => new(growthVariation.x, growthVariation.y, 1f / growthVariationSize, edgeGrowth);

        /// <summary>Metres the canopy rises to at most: the height times the tallest growth.</summary>
        public float CanopyRise => height * Mathf.Max(1f, growthVariation.y);

        /// <summary>The nearest the canopy starts to rise: at the flattest view the hand-off band reaches furthest in.</summary>
        public float CanopyStart => lod1Distance - canopyFade * (1f + GrazingWiden);

        /// <summary>Lodging as the kernel and the canopy read it: patch share, smallest and largest patch across.</summary>
        public Vector4 Lodging => new(lodging, lodgedSize.x, lodgedSize.y, 0f);

        /// <summary>Problems that keep the crop from drawing, one per line. Empty when ready.</summary>
        public IReadOnlyList<string> Problems()
        {
            var problems = new List<string>();
            Variants(problems);
            Require(problems, material != null, $"{nameof(material)} is missing");
            Require(problems, rowSpacing > 0f, $"{nameof(rowSpacing)} {rowSpacing} must be above 0");
            Require(problems, plantSpacing > 0f, $"{nameof(plantSpacing)} {plantSpacing} must be above 0");
            Require(problems, stand is >= 0.5f and <= 1f, $"{nameof(stand)} {stand} must be 0.5 to 1");
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
            Require(problems, edgeGrowth is >= 0.3f and <= 1f, $"{nameof(edgeGrowth)} {edgeGrowth} must be 0.3 to 1");
            Require(problems, edgeMargin >= 0f, $"{nameof(edgeMargin)} {edgeMargin} must not be negative");
            Require(problems, lodgedSize.x > 0f && lodgedSize.y >= lodgedSize.x && lodgedSize.y < LodgingCell,
                $"{nameof(lodgedSize)} {lodgedSize} must be above 0, y at least x and under {LodgingCell}");
            Require(problems, height > 0f, $"{nameof(height)} {height} must be above 0");
            Require(problems, lod0Distance > 0f, $"{nameof(lod0Distance)} {lod0Distance} must be above 0");
            Require(problems, farShare is >= 0.05f and <= 1f, $"{nameof(farShare)} {farShare} must be 0.05 to 1");
            Require(problems, farScale is >= 1f and <= 2f, $"{nameof(farScale)} {farScale} must be 1 to 2");
            Require(problems, thinStart >= 0f && thinStart < lod1Distance,
                $"{nameof(thinStart)} {thinStart} must be 0 to {nameof(lod1Distance)} {lod1Distance}");
            Require(problems, lod1Distance > lod0Distance,
                $"{nameof(lod1Distance)} {lod1Distance} must be above {nameof(lod0Distance)} {lod0Distance}");
            // An empty canopyMaterial is a choice, not a gap: the plants alone draw the crop and its tile goes unused.
            Require(problems, !HasCanopy || (canopyRows >= 1 && canopyPlants >= 1),
                $"{nameof(canopyRows)} {canopyRows} and {nameof(canopyPlants)} {canopyPlants} must be at least 1");
            Require(problems, canopyFade > 0f && CanopyStart >= lod0Distance,
                $"{nameof(canopyFade)} {canopyFade} must be above 0 and end the fade no nearer than {nameof(lod0Distance)} {lod0Distance}");
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
