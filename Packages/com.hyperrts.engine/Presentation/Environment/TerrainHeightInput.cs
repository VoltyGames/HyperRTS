using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>Binds a terrain's heightmap to a scatter kernel as Scatter.hlsl reads it.</summary>
    internal readonly struct TerrainHeightInput
    {
        // Unity heightmaps store 32766 as full height in a 16-bit unorm texture.
        private const float HeightmapFull = 32766f / 65535f;

        private static readonly int HeightId = Shader.PropertyToID("_Height");
        private static readonly int HeightAreaId = Shader.PropertyToID("_HeightArea");
        private static readonly int HeightScaleId = Shader.PropertyToID("_HeightScale");

        private readonly Terrain _terrain;
        private readonly Texture _heights;

        /// <summary><c>_HeightScale</c> is base height, metres per unit, texels across.</summary>
        public TerrainHeightInput(ComputeShader compute, int kernel, Terrain terrain)
        {
            var data = terrain.terrainData;
            var origin = terrain.GetPosition();
            var size = data.size;
            _terrain = terrain;
            _heights = data.heightmapTexture;
            compute.SetTexture(kernel, HeightId, _heights);
            compute.SetVector(HeightAreaId, InteractionMath.Rect(new float2(origin.x, origin.z), new float2(size.x, size.z)));
            compute.SetVector(HeightScaleId, new Vector4(origin.y, size.y / HeightmapFull, data.heightmapResolution, 0f));
        }

        /// <summary>Whether a map rebuild replaced the heightmap bound to the kernel.</summary>
        public bool Stale => _heights != _terrain.terrainData.heightmapTexture;
    }
}
