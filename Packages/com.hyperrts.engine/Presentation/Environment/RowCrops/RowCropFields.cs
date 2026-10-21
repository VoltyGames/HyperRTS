using System.Collections.Generic;
using HyperRTS.Core;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.RowCrops
{
    /// <summary>A map's baked row crops: field-id picture, row lattices, crops and the tiles that hold any.</summary>
    [Icon(HyperRTSIcons.Environment)]
    [HelpURL(HyperRTSDocs.Presentation)]
    public sealed class RowCropFields : ScriptableObject
    {
        /// <summary>Side of a crop tile in metres: the unit the renderer culls and generates.</summary>
        public const float TileSize = 16f;

        /// <summary>Field id 0: no crop here.</summary>
        public const int None = 0;

        [Tooltip("Field id per texel, north up: R low byte, G high byte. Point-sampled, linear.")]
        public Texture2D fieldIds;

        [Tooltip("World XZ rectangle the picture and tile grid cover. x and y are its minimum x and z.")]
        public Rect area;

        public List<RowCrop> crops = new();

        public List<RowCropField> fields = new();

        public List<RowCropTile> tiles = new();

        [Tooltip("Field indices per tile, each tile's run in turn.")]
        public List<int> tileFields = new();

        /// <summary>The id the picture holds for a field: its index plus one.</summary>
        public static int FieldId(int field) => field + 1;

        /// <summary>A field id as the picture stores it.</summary>
        public static Color32 Encode(int id) => new((byte)(id & 0xFF), (byte)(id >> 8), 0, 255);

        public static int Decode(Color32 texel) => texel.r | (texel.g << 8);

        public Rect TileRect(RowCropTile tile) =>
            new(area.xMin + tile.x * TileSize, area.yMin + tile.z * TileSize, TileSize, TileSize);

        /// <summary>Metres the tallest crop can grow to, the height of every tile's culling box.</summary>
        public float TallestPlant
        {
            get
            {
                var height = 0f;
                crops.ForEach(crop => height = Mathf.Max(height, crop.height * Mathf.Max(1f, crop.growthVariation.y)));
                return height;
            }
        }

        /// <summary>Problems that keep the fields from drawing, one per line. Empty when ready.</summary>
        public IReadOnlyList<string> Problems()
        {
            var problems = new List<string>();
            if (fieldIds == null)
            {
                problems.Add($"{nameof(fieldIds)} is missing");
            }

            if (area.width <= 0f || area.height <= 0f)
            {
                problems.Add($"{nameof(area)} {area} has no size");
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

            return problems;
        }
    }
}
