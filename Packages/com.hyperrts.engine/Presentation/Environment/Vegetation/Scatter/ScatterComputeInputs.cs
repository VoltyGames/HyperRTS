using System;
using HyperRTS.Presentation.Environment.Ground;
using HyperRTS.Presentation.Environment.Ground.Craters;
using HyperRTS.Presentation.Environment.Ground.Stamps;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Vegetation.Scatter
{
    /// <summary>
    /// What a scatter compute shader reads: the items, tile size, heightmap and each camera's view. Everything is bound before
    /// each dispatch, as reimporting the shader drops its bindings.
    /// </summary>
    internal abstract class ScatterComputeInputs : IDisposable
    {
        private static readonly int CameraId = Shader.PropertyToID("_Camera");
        private static readonly int TileSizeId = Shader.PropertyToID("_TileSize");

        private readonly TerrainHeightInput _heights;

        /// <summary>Finds the kernels and makes the items buffer, which the subclass fills.</summary>
        protected ScatterComputeInputs(ComputeShader compute, string growKernel, Terrain terrain, int items, int stride)
        {
            Compute = compute;
            GrowKernel = compute.FindKernel(growKernel);
            CountKernel = compute.FindKernel("Count");
            Items = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, items), stride);
            _heights = new TerrainHeightInput(terrain);
        }

        public ComputeShader Compute { get; }

        /// <summary>The kernel that grows instances into the slots.</summary>
        public int GrowKernel { get; }

        /// <summary>The kernel that turns the grown instances into draw args.</summary>
        public int CountKernel { get; }

        /// <summary>Whether a map rebuild changed the items, the subclass's own inputs or the heightmap.</summary>
        public bool Stale => Items.count != Mathf.Max(1, ItemCount) || OwnInputsStale || _heights.Stale;

        /// <summary>The baked items (plants or fields) uploaded once per build.</summary>
        protected GraphicsBuffer Items { get; }

        /// <summary>How many items the baked asset holds now.</summary>
        protected abstract int ItemCount { get; }

        protected abstract bool OwnInputsStale { get; }

        /// <summary>Binds one camera's view, the map's inputs and the subclass's.</summary>
        public void SetCamera(Vector3 position, ScatterSettings settings)
        {
            Compute.SetFloat(TileSizeId, ScatterTiles.Size);
            _heights.Bind(Compute, GrowKernel);
            Compute.SetVector(CameraId, new Vector4(position.x, position.y, position.z, settings.Distance));
            BuildingFootprintMask.Bind(Compute, GrowKernel);
            CraterTerrain.BindCraters(Compute, GrowKernel);
            TrampleSurfaces.RemainsGlobal.Bind(Compute, GrowKernel);
            BindCamera(settings);
        }

        /// <summary>Binds the subclass's items, maps and quality knobs.</summary>
        protected abstract void BindCamera(ScatterSettings settings);

        public void Dispose() => Items.Release();
    }
}
