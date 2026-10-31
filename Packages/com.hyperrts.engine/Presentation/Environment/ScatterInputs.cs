using HyperRTS.Presentation.Environment.Grass;
using UnityEngine;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>
    /// What both scatter kernels read: the tile size, the heightmap, and each camera's view with the building mask,
    /// crater depth and ash. The maps are copied from the globals because compute shaders don't see them.
    /// </summary>
    internal sealed class ScatterInputs
    {
        private static readonly int CameraId = Shader.PropertyToID("_Camera");
        private static readonly int TileSizeId = Shader.PropertyToID("_TileSize");

        private readonly ComputeShader _compute;
        private readonly int _kernel;
        private readonly TerrainHeightInput _heights;

        /// <summary>Binds what stays fixed while the map is open to <paramref name="kernel"/>.</summary>
        public ScatterInputs(ComputeShader compute, int kernel, Terrain terrain)
        {
            _compute = compute;
            _kernel = kernel;
            compute.SetFloat(TileSizeId, ScatterTiles.Size);
            _heights = new TerrainHeightInput(compute, kernel, terrain);
        }

        /// <summary>Whether a map rebuild replaced the heightmap bound to the kernel.</summary>
        public bool Stale => _heights.Stale;

        /// <summary>Binds one camera's position and draw distance, and the building mask, crater depth and ash.</summary>
        public void BindCamera(Vector3 position, float distance)
        {
            _compute.SetVector(CameraId, new Vector4(position.x, position.y, position.z, distance));
            BuildingFootprintMask.Bind(_compute, _kernel);
            CraterTerrain.BindCraters(_compute, _kernel);
            InteractionSurfaces.BindRemains(_compute, _kernel);
        }
    }
}
