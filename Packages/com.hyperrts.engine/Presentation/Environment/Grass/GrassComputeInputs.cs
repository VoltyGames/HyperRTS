using System;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Grass
{
    /// <summary>The Grass compute shader's kernels and what they read. The ground picture is copied from the globals.</summary>
    internal sealed class GrassComputeInputs : IDisposable
    {
        private static readonly int PlantsId = Shader.PropertyToID("_Plants");
        private static readonly int CoverageId = Shader.PropertyToID("_Coverage");
        private static readonly int CoverageAreaId = Shader.PropertyToID("_CoverageArea");
        private static readonly int ThinningId = Shader.PropertyToID("_Thinning");
        private static readonly int ShapingId = Shader.PropertyToID("_Shaping");

        private readonly GrassField _field;
        private readonly GraphicsBuffer _plants;
        private readonly ScatterInputs _scatter;
        private readonly Texture _coverage;

        /// <summary>Uploads the plants and binds what stays fixed while the map is open.</summary>
        public GrassComputeInputs(ComputeShader compute, GrassField field, Terrain terrain)
        {
            Compute = compute;
            Scatter = compute.FindKernel(nameof(Scatter));
            Count = compute.FindKernel(nameof(Count));
            _field = field;
            _coverage = field.coverage;
            _plants = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, field.plants.Count),
                GrassPlantGpu.Stride);
            _plants.SetData(field.GpuPlants());
            compute.SetBuffer(Scatter, PlantsId, _plants);
            compute.SetTexture(Scatter, CoverageId, _coverage);
            var area = field.area;
            compute.SetVector(CoverageAreaId, TerrainHeightInput.Rect(new float2(area.xMin, area.yMin), new float2(area.width, area.height)));
            compute.SetVector(ThinningId, GrassBands.Thinning);
            _scatter = new ScatterInputs(compute, Scatter, terrain);
        }

        public ComputeShader Compute { get; }

        public int Scatter { get; }

        public int Count { get; }

        /// <summary>Whether a map rebuild changed the plants, the coverage or the heightmap bound to the kernel.</summary>
        public bool Stale => _plants.count != Mathf.Max(1, _field.plants.Count) || _coverage != _field.coverage || _scatter.Stale;

        /// <summary>Binds one camera's view, the quality knobs, the ground picture, the building mask, crater depth and ash.</summary>
        public void SetCamera(Vector3 camera, ScatterSettings settings)
        {
            Compute.SetVector(ShapingId, new Vector4(settings.Density, settings.ShadowDistance, GrassBands.KeepEdge, 0f));
            _scatter.BindCamera(camera, settings.Distance);
            GroundColor.Bind(Compute, Scatter);
        }

        public void Dispose() => _plants.Release();
    }
}
