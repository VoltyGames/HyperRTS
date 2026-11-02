using HyperRTS.Presentation.Environment.Ground;
using HyperRTS.Presentation.Environment.Vegetation.Scatter;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Vegetation.Grass
{
    /// <summary>What Grass.compute reads beside the shared inputs: the plants, the coverage map and the ground picture.</summary>
    internal sealed class GrassComputeInputs : ScatterComputeInputs
    {
        private static readonly int PlantsId = Shader.PropertyToID("_Plants");
        private static readonly int CoverageId = Shader.PropertyToID("_Coverage");
        private static readonly int CoverageAreaId = Shader.PropertyToID("_CoverageArea");
        private static readonly int ThinningId = Shader.PropertyToID("_Thinning");
        private static readonly int ShapingId = Shader.PropertyToID("_Shaping");

        private readonly GrassField _field;
        private readonly Texture _coverage;

        /// <summary>Uploads the plants.</summary>
        public GrassComputeInputs(ComputeShader compute, GrassField field, Terrain terrain)
            : base(compute, "Scatter", terrain, field.plants.Count, GrassPlantGpu.Stride)
        {
            _field = field;
            _coverage = field.coverage;
            Items.SetData(field.GpuPlants());
        }

        protected override int ItemCount => _field.plants.Count;

        protected override bool OwnInputsStale => _coverage != _field.coverage;

        /// <summary>Binds the plants, their coverage, the quality knobs and the ground picture.</summary>
        protected override void BindCamera(ScatterSettings settings)
        {
            var area = _field.area;
            Compute.SetBuffer(GrowKernel, PlantsId, Items);
            Compute.SetTexture(GrowKernel, CoverageId, _coverage);
            Compute.SetVector(CoverageAreaId,
                TerrainHeightInput.Rect(new float2(area.xMin, area.yMin), new float2(area.width, area.height)));
            Compute.SetVector(ThinningId, GrassBands.Thinning);
            Compute.SetVector(ShapingId, new Vector4(settings.Density, settings.ShadowDistance, GrassBands.KeepEdge,
                GrassBands.TallestPatch - 1f));
            GroundColor.Bind(Compute, GrowKernel);
        }
    }
}
