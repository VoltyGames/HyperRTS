using System.Collections.Generic;
using HyperRTS.Presentation.Environment.Vegetation.Scatter;
using HyperRTS.Simulation.Common.Authoring;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Vegetation.RowCrops
{
    /// <summary>A map's baked row crops: field-id picture, row lattices, crops and the tiles that hold any.</summary>
    [Icon(HyperRTSIcons.RowCropFields)]
    [HelpURL(HyperRTSDocs.Presentation)]
    public sealed class RowCropFields : ScatterField
    {
        /// <summary>Field id 0: no crop here.</summary>
        public const int None = 0;

        [Tooltip("Field id per texel, north up: R low byte, G high byte. Point-sampled, linear.")]
        public Texture2D fieldIds;

        public List<RowCrop> crops = new();

        public List<RowCropField> fields = new();

        [Tooltip("Field indices per tile, each tile's run in turn.")]
        public List<int> tileFields = new();

        /// <summary>The id the picture holds for a field: its index plus one.</summary>
        public static int FieldId(int field) => field + 1;

        /// <summary>A field id as the picture stores it.</summary>
        public static Color32 Encode(int id) => new((byte)(id & 0xFF), (byte)(id >> 8), 0, 255);

        public static int Decode(Color32 texel) => texel.r | (texel.g << 8);

        /// <summary>Metres the tallest crop can grow to, the height of every tile's culling box.</summary>
        public override float TallestPlant
        {
            get
            {
                var height = 0f;
                crops.ForEach(crop => height = Mathf.Max(height, crop.height * Mathf.Max(1f, crop.growthVariation.y)));
                return height;
            }
        }

        private protected override void AddProblems(List<string> problems)
        {
            if (fieldIds == null)
            {
                problems.Add($"{nameof(fieldIds)} is missing");
            }

            for (var i = 0; i < crops.Count; i++)
            {
                if (crops[i] == null)
                {
                    problems.Add($"{nameof(crops)}[{i}] is missing");
                    continue;
                }

                foreach (var problem in crops[i].Problems())
                {
                    problems.Add($"crop '{crops[i].name}': {problem}");
                }
            }
        }
    }
}
