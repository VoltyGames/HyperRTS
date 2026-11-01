using System;
using HyperRTS.Presentation.Environment.Ground;
using HyperRTS.Presentation.Environment.Trampling;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Vegetation.Scatter
{
    /// <summary>
    /// A scatter compute shader's grow and count kernels and what every grow kernel reads: the uploaded items, the tile
    /// size, the heightmap, and each camera's view with the building mask, crater depth and ash. Subclasses add their
    /// own inputs.
    /// </summary>
    internal abstract class ScatterComputeInputs : IDisposable
    {
        private static readonly int CameraId = Shader.PropertyToID("_Camera");
        private static readonly int TileSizeId = Shader.PropertyToID("_TileSize");

        private readonly TerrainHeightInput _heights;

        /// <summary>Finds the kernels and binds what stays fixed while the map is open.</summary>
        protected ScatterComputeInputs(ComputeShader compute, string growKernel, Terrain terrain, int items, int stride)
        {
            Compute = compute;
            Grow = compute.FindKernel(growKernel);
            Count = compute.FindKernel(nameof(Count));
            Items = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, items), stride);
            compute.SetFloat(TileSizeId, ScatterTiles.Size);
            _heights = new TerrainHeightInput(compute, Grow, terrain);
        }

        public ComputeShader Compute { get; }

        /// <summary>The kernel that grows instances into the slots.</summary>
        public int Grow { get; }

        /// <summary>The kernel that turns the grown instances into draw args.</summary>
        public int Count { get; }

        /// <summary>Whether a map rebuild changed the items, the subclass's own inputs or the heightmap.</summary>
        public bool Stale => Items.count != Mathf.Max(1, ItemCount) || OwnInputsStale || _heights.Stale;

        /// <summary>The baked items (plants or fields) uploaded once per build.</summary>
        protected GraphicsBuffer Items { get; }

        /// <summary>How many items the baked asset holds now.</summary>
        protected abstract int ItemCount { get; }

        protected abstract bool OwnInputsStale { get; }

        /// <summary>Binds one camera's view, the shared maps and the subclass's per-camera inputs.</summary>
        public void SetCamera(Vector3 position, ScatterSettings settings)
        {
            Compute.SetVector(CameraId, new Vector4(position.x, position.y, position.z, settings.Distance));
            BuildingFootprintMask.Bind(Compute, Grow);
            CraterTerrain.BindCraters(Compute, Grow);
            TrampleSurfaces.RemainsGlobal.Bind(Compute, Grow);
            BindCamera(settings);
        }

        protected abstract void BindCamera(ScatterSettings settings);

        public void Dispose() => Items.Release();
    }
}
