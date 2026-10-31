using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Presentation.Environment.Vegetation.Scatter;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Vegetation.Grass
{
    /// <summary>A map's baked grass and small plants: a coverage slice per plant, the plants and the tiles that grow any.</summary>
    [Icon(HyperRTSIcons.Environment)]
    [HelpURL(HyperRTSDocs.Presentation)]
    public sealed class GrassField : ScatterField
    {
        [Tooltip("Share of the ground each plant covers, 0 to 1, one slice per plant in plant order, north up.")]
        public Texture2DArray coverage;

        public List<GrassPlant> plants = new();

        [Tooltip("Plants per tile with their mean coverage, each tile's run in turn.")]
        public List<GrassTilePlant> tilePlants = new();

        /// <summary>Draw slots over all plants: one per model and band, in plant order.</summary>
        public int SlotCount => FirstSlots()[plants.Count];

        /// <summary>Metres the tallest plant can grow to, thinning's growth included, the height of every tile's culling box.</summary>
        public override float TallestPlant
        {
            get
            {
                var height = 0f;
                plants.ForEach(plant => height = Mathf.Max(height, plant.Tallest * GrassBands.MaxGrow * GrassBands.TallestPatch));
                return height;
            }
        }

        /// <summary>Each plant's first draw slot in plant order, then the slot count: a plant takes one per model and band.</summary>
        public int[] FirstSlots()
        {
            var first = new int[plants.Count + 1];
            for (var p = 0; p < plants.Count; p++)
            {
                first[p + 1] = first[p] + plants[p].meshes.Count * GrassBands.Bands;
            }

            return first;
        }

        /// <summary>Every plant as Grass.compute reads it, with its first draw slot.</summary>
        public GrassPlantGpu[] GpuPlants()
        {
            var first = FirstSlots();
            var gpuPlants = new GrassPlantGpu[plants.Count];
            for (var i = 0; i < plants.Count; i++)
            {
                gpuPlants[i] = plants[i].ToGpu(i, first[i]);
            }

            return gpuPlants;
        }

        private protected override void AddProblems(List<string> problems)
        {
            if (coverage == null)
            {
                problems.Add($"{nameof(coverage)} is missing");
            }
            else if (coverage.depth != plants.Count)
            {
                problems.Add($"{nameof(coverage)} has {coverage.depth} slices for {plants.Count} plants");
            }

            for (var i = 0; i < plants.Count; i++)
            {
                foreach (var problem in plants[i].Problems())
                {
                    problems.Add($"plant '{plants[i].name}': {problem}");
                }
            }
        }
    }
}
