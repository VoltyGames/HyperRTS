using System;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.RowCrops
{
    /// <summary>What RowCrops.compute reads. The trample map is copied from the globals: compute shaders don't see them.</summary>
    internal sealed class RowCropKernelInputs : IDisposable
    {
        // Unity heightmaps store 32766 as full height in a 16-bit unorm texture.
        private const float HeightmapFull = 32766f / 65535f;

        private static readonly int FieldsId = Shader.PropertyToID("_Fields");
        private static readonly int FieldIdsId = Shader.PropertyToID("_FieldIds");
        private static readonly int FieldAreaId = Shader.PropertyToID("_FieldArea");
        private static readonly int HeightId = Shader.PropertyToID("_Height");
        private static readonly int HeightAreaId = Shader.PropertyToID("_HeightArea");
        private static readonly int HeightScaleId = Shader.PropertyToID("_HeightScale");
        private static readonly int TrampleId = Shader.PropertyToID("_Trample");
        private static readonly int TrampleRectId = Shader.PropertyToID("_TrampleRect");
        private static readonly int CameraId = Shader.PropertyToID("_Camera");
        private static readonly int TileSizeId = Shader.PropertyToID("_TileSize");
        private static readonly int DensityId = Shader.PropertyToID("_Density");

        private readonly ComputeShader _compute;
        private readonly int _kernel;
        private readonly RowCropFields _fields;
        private readonly Terrain _terrain;
        private readonly GraphicsBuffer _fieldBuffer;

        public RowCropKernelInputs(ComputeShader compute, int kernel, RowCropFields fields, Terrain terrain)
        {
            _compute = compute;
            _kernel = kernel;
            _fields = fields;
            _terrain = terrain;
            _fieldBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, fields.fields.Count),
                RowCropField.Stride);
            _fieldBuffer.SetData(fields.fields);
        }

        /// <summary>Binds everything for this frame's dispatches.</summary>
        public void Set(Vector3 camera, float density, float distance)
        {
            var ids = _fields.fieldIds;
            var area = _fields.area;
            _compute.SetBuffer(_kernel, FieldsId, _fieldBuffer);
            _compute.SetTexture(_kernel, FieldIdsId, ids);
            _compute.SetVector(FieldAreaId, new Vector4(area.xMin, area.yMin, ids.width / area.width, ids.height / area.height));
            _compute.SetFloat(TileSizeId, RowCropFields.TileSize);
            _compute.SetVector(CameraId, new Vector4(camera.x, camera.y, camera.z, distance));
            _compute.SetFloat(DensityId, density);
            SetHeights();
            SetTrample();
        }

        public void Dispose() => _fieldBuffer.Release();

        /// <summary>The heightmap and how to read it: <c>_HeightScale</c> is base height, metres per unit, texels across.</summary>
        private void SetHeights()
        {
            var data = _terrain.terrainData;
            var origin = _terrain.GetPosition();
            var size = data.size;
            _compute.SetTexture(_kernel, HeightId, data.heightmapTexture);
            _compute.SetVector(HeightAreaId, new Vector4(origin.x, origin.z, 1f / size.x, 1f / size.z));
            _compute.SetVector(HeightScaleId, new Vector4(origin.y, size.y / HeightmapFull, data.heightmapResolution, 0f));
        }

        private void SetTrample()
        {
            var trample = Shader.GetGlobalTexture(InteractionSurfaces.TrampleId);
            var rect = Shader.GetGlobalVector(InteractionSurfaces.RectId);
            var on = trample != null && rect.z > 0f;
            _compute.SetTexture(_kernel, TrampleId, on ? trample : Texture2D.blackTexture);
            _compute.SetVector(TrampleRectId, on ? rect : Vector4.zero);
        }
    }
}
