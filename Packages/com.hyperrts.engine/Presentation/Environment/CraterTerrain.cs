using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>
    /// A Unity Terrain dented on the GPU by the crater bowls in the tracks map, with a copy of its heights from before
    /// the match to put back. Visual only: the simulation's ground never reads it.
    /// </summary>
    public sealed class CraterTerrain : IDisposable
    {
        /// <summary>Where <c>Resources.Load</c> finds the shader that dents the heightmap.</summary>
        public const string ShaderPath = "HyperRTS/CraterHeight";

        private static readonly int OriginalHeightsId = Shader.PropertyToID("_OriginalHeights");
        private static readonly int HeightRegionId = Shader.PropertyToID("_HeightRegion");
        private static readonly int TerrainTexelsId = Shader.PropertyToID("_TerrainTexels");
        private static readonly int DepthScaleId = Shader.PropertyToID("_DepthScale");

        /// <summary>The heights from before any crater, which sinking shaders measure an object's height above.</summary>
        public static readonly int UndentedHeightsId = Shader.PropertyToID("_HyperRTSUndentedHeights");
        public static readonly int UndentedAreaId = Shader.PropertyToID("_HyperRTSUndentedArea");
        public static readonly int UndentedScaleId = Shader.PropertyToID("_HyperRTSUndentedScale");

        private readonly TerrainData _data;
        private readonly RenderTexture _original;
        private readonly Material _material;
        private readonly CommandBuffer _commands = new() { name = "HyperRTS Crater Terrain" };
        private readonly float2 _origin;
        private readonly float2 _texelsPerMetre;
        private RectInt _unsynced;
        private bool _dented;

        public CraterTerrain(Terrain terrain, Shader shader)
        {
            Terrain = terrain;
            _data = terrain.terrainData;
            var heights = _data.heightmapTexture;
            _original = new RenderTexture(heights.width, heights.height, heights.graphicsFormat, GraphicsFormat.None, 1)
            {
                name = "HyperRTS Original Heights", hideFlags = HideFlags.DontSave, filterMode = FilterMode.Point,
            };
            _original.Create();
            Graphics.CopyTexture(heights, 0, 0, _original, 0, 0);

            var position = terrain.GetPosition();
            var size = _data.size;
            _origin = new float2(position.x, position.z);
            _texelsPerMetre = (_data.heightmapResolution - 1) / new float2(size.x, size.z);
            _material = CoreUtils.CreateEngineMaterial(shader);
            _material.SetTexture(OriginalHeightsId, _original);
            _material.SetVector(TerrainTexelsId, new float4(_origin, 1f / _texelsPerMetre));
            _material.SetFloat(DepthScaleId, TerrainHeightInput.HeightmapFull / size.y);
            Shader.SetGlobalTexture(UndentedHeightsId, _original);
            Shader.SetGlobalVector(UndentedAreaId, TerrainHeightInput.Area(terrain));
            Shader.SetGlobalVector(UndentedScaleId, TerrainHeightInput.Scale(terrain));
        }

        public Terrain Terrain { get; }

        /// <summary>Copies the crater depth globals to a kernel: compute shaders don't see globals.</summary>
        public static void BindCraters(ComputeShader compute, int kernel)
        {
            var tracks = Shader.GetGlobalTexture(InteractionSurfaces.TracksId);
            var rect = Shader.GetGlobalVector(InteractionSurfaces.RectId);
            var on = tracks != null && rect.z > 0f;
            compute.SetTexture(kernel, InteractionSurfaces.TracksId, on ? tracks : Texture2D.blackTexture);
            compute.SetVector(InteractionSurfaces.RectId, on ? rect : Vector4.zero);
            compute.SetFloat(InteractionSurfaces.MaxCraterDepthId, InteractionMath.MaxCraterDepth);
        }

        /// <summary>Dented on the GPU but not yet read back into the terrain's heights, collider and LOD.</summary>
        public bool Unsynced => _unsynced.width > 0;

        /// <summary>Redraws the heights under a world XZ box from the originals and the tracks map's crater depth.</summary>
        public void Dent(float2 min, float2 max)
        {
            var region = Texels(min, max);
            if (region.width <= 0 || region.height <= 0)
            {
                return;
            }

            var target = RenderTexture.GetTemporary(new RenderTextureDescriptor(region.width, region.height,
                _original.graphicsFormat, GraphicsFormat.None));
            _material.SetVector(HeightRegionId, new Vector4(region.x, region.y, 0f, 0f));
            _commands.Clear();
            _commands.SetRenderTarget(target);
            _commands.DrawProcedural(Matrix4x4.identity, _material, 0, MeshTopology.Triangles, 3);
            Graphics.ExecuteCommandBuffer(_commands);
            Copy(target, region);
            RenderTexture.ReleaseTemporary(target);
            _dented = true;
        }

        /// <summary>Reads the dented heights back, so Unity refines the terrain mesh there. The costly step.</summary>
        public void Sync()
        {
            if (!Unsynced)
            {
                return;
            }

            _data.DirtyHeightmapRegion(_unsynced, TerrainHeightmapSyncControl.HeightAndLod);
            _data.SyncHeightmap();
            _unsynced = default;
        }

        /// <summary>Puts the heights from before the first crater back, so none are saved or outlive a replay seek.</summary>
        public void Restore()
        {
            if (!_dented)
            {
                return;
            }

            Copy(_original, new RectInt(0, 0, _original.width, _original.height));
            Sync();
            _dented = false;
        }

        public void Dispose()
        {
            // A terrain unloaded with its scene may have taken its TerrainData with it.
            if (_data != null)
            {
                Restore();
            }

            Shader.SetGlobalTexture(UndentedHeightsId, Texture2D.blackTexture);
            Shader.SetGlobalVector(UndentedAreaId, Vector4.zero);
            Shader.SetGlobalVector(UndentedScaleId, Vector4.zero);
            _commands.Release();
            _original.Release();
            CoreUtils.Destroy(_original);
            CoreUtils.Destroy(_material);
        }

        private void Copy(RenderTexture source, RectInt region)
        {
            var active = RenderTexture.active;
            RenderTexture.active = source;
            _data.CopyActiveRenderTextureToHeightmap(new RectInt(0, 0, region.width, region.height), region.position,
                TerrainHeightmapSyncControl.None);
            RenderTexture.active = active;
            _unsynced = Unsynced ? Union(_unsynced, region) : region;
        }

        // One texel of margin, as the shaders sample the tracks map bilinearly.
        private RectInt Texels(float2 min, float2 max)
        {
            var last = _data.heightmapResolution - 1;
            var low = math.clamp((int2)math.floor((min - _origin) * _texelsPerMetre) - 1, 0, last);
            var high = math.clamp((int2)math.ceil((max - _origin) * _texelsPerMetre) + 1, 0, last);
            return new RectInt(low.x, low.y, high.x - low.x + 1, high.y - low.y + 1);
        }

        private static RectInt Union(RectInt a, RectInt b)
        {
            var min = Vector2Int.Min(a.min, b.min);
            var max = Vector2Int.Max(a.max, b.max);
            return new RectInt(min, max - min);
        }
    }
}
