using System;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Grass
{
    /// <summary>The Grass compute shader's kernels and what they read. The ground picture is copied from the globals.</summary>
    internal sealed class GrassKernelInputs : IDisposable
    {
        private static readonly int PlantsId = Shader.PropertyToID("_Plants");
        private static readonly int CoverageId = Shader.PropertyToID("_Coverage");
        private static readonly int CoverageAreaId = Shader.PropertyToID("_CoverageArea");
        private static readonly int GroundId = Shader.PropertyToID("_Ground");
        private static readonly int GroundAreaId = Shader.PropertyToID("_GroundArea");
        private static readonly int CameraId = Shader.PropertyToID("_Camera");
        private static readonly int ThinningId = Shader.PropertyToID("_Thinning");
        private static readonly int ShapingId = Shader.PropertyToID("_Shaping");
        private static readonly int TileSizeId = Shader.PropertyToID("_TileSize");

        private readonly GrassField _field;
        private readonly GraphicsBuffer _plants;
        private readonly TerrainHeightInput _heights;
        private readonly Texture _coverage;

        /// <summary>Uploads the plants and binds what stays fixed while the map is open.</summary>
        public GrassKernelInputs(ComputeShader compute, GrassField field, Terrain terrain)
        {
            Compute = compute;
            Scatter = compute.FindKernel(nameof(Scatter));
            Count = compute.FindKernel(nameof(Count));
            _field = field;
            _coverage = field.coverage;
            _plants = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, field.plants.Count),
                GrassSowing.Stride);
            _plants.SetData(field.Sowings());
            compute.SetBuffer(Scatter, PlantsId, _plants);
            compute.SetTexture(Scatter, CoverageId, _coverage);
            var area = field.area;
            compute.SetVector(CoverageAreaId, InteractionMath.Rect(new float2(area.xMin, area.yMin), new float2(area.width, area.height)));
            compute.SetVector(ThinningId, GrassView.Thinning);
            compute.SetFloat(TileSizeId, GrassField.TileSize);
            _heights = new TerrainHeightInput(compute, Scatter, terrain);
        }

        public ComputeShader Compute { get; }

        public int Scatter { get; }

        public int Count { get; }

        /// <summary>Whether a map rebuild changed the plants, the coverage or the heightmap bound to the kernel.</summary>
        public bool Stale => _plants.count != Mathf.Max(1, _field.plants.Count) || _coverage != _field.coverage || _heights.Stale;

        /// <summary>Binds one camera's view, the quality knobs and the current ground picture for its dispatches.</summary>
        public void SetCamera(Vector3 camera, float density, float distance, float shadowDistance)
        {
            Compute.SetVector(CameraId, new Vector4(camera.x, camera.y, camera.z, distance));
            Compute.SetVector(ShapingId, new Vector4(density, shadowDistance, GrassView.KeepEdge, 0f));
            var ground = Shader.GetGlobalTexture(GroundColor.PictureId);
            var rect = Shader.GetGlobalVector(GroundColor.RectId);
            var on = ground != null && rect.z > 0f;
            Compute.SetTexture(Scatter, GroundId, on ? ground : Texture2D.blackTexture);
            Compute.SetVector(GroundAreaId, on ? rect : Vector4.zero);
        }

        public void Dispose() => _plants.Release();
    }
}
