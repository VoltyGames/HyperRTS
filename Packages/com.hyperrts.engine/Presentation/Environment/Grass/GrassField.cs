using System.Collections.Generic;
using HyperRTS.Core;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Grass
{
    /// <summary>A map's baked grass and small plants: a coverage slice per plant, the plants and the tiles that grow any.</summary>
    [Icon(HyperRTSIcons.Environment)]
    [HelpURL(HyperRTSDocs.Presentation)]
    public sealed class GrassField : ScriptableObject
    {
        /// <summary>Side of a grass tile in metres: the unit the renderer culls and scatters.</summary>
        public const float TileSize = 16f;

        [Tooltip("Share of the ground each plant covers, 0 to 1, one slice per plant in plant order, north up.")]
        public Texture2DArray coverage;

        [Tooltip("World XZ rectangle the coverage and tile grid cover. x and y are its minimum x and z.")]
        public Rect area;

        public List<GrassPlant> plants = new();

        public List<GrassTile> tiles = new();

        [Tooltip("Plants per tile with their mean coverage, each tile's run in turn.")]
        public List<GrassTilePlant> tilePlants = new();

        public Rect TileRect(GrassTile tile) =>
            new(area.xMin + tile.x * TileSize, area.yMin + tile.z * TileSize, TileSize, TileSize);

        /// <summary>Draw slots over all plants: one per model and band, in plant order.</summary>
        public int SlotCount
        {
            get
            {
                var slots = 0;
                plants.ForEach(plant => slots += plant.meshes.Count * GrassView.Bands);
                return slots;
            }
        }

        /// <summary>Every plant as Grass.compute reads it, with its first draw slot.</summary>
        public GrassSowing[] Sowings()
        {
            var sowings = new GrassSowing[plants.Count];
            var slot = 0;
            for (var i = 0; i < plants.Count; i++)
            {
                sowings[i] = plants[i].Sowing(i, slot);
                slot += plants[i].meshes.Count * GrassView.Bands;
            }

            return sowings;
        }

        /// <summary>Problems that keep the field from drawing, one per line. Empty when ready.</summary>
        public IReadOnlyList<string> Problems()
        {
            var problems = new List<string>();
            if (coverage == null)
            {
                problems.Add($"{nameof(coverage)} is missing");
            }
            else if (coverage.depth != plants.Count)
            {
                problems.Add($"{nameof(coverage)} has {coverage.depth} slices for {plants.Count} plants");
            }

            if (area.width <= 0f || area.height <= 0f)
            {
                problems.Add($"{nameof(area)} {area} has no size");
            }

            for (var i = 0; i < plants.Count; i++)
            {
                foreach (var problem in plants[i].Problems())
                {
                    problems.Add($"plant '{plants[i].name}': {problem}");
                }
            }

            return problems;
        }
    }
}
