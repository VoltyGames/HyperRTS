using System;
using Unity.Mathematics;
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
        private static readonly int FieldTexelsId = Shader.PropertyToID("_FieldTexels");
        private static readonly int HeightId = Shader.PropertyToID("_Height");
        private static readonly int HeightAreaId = Shader.PropertyToID("_HeightArea");
        private static readonly int HeightScaleId = Shader.PropertyToID("_HeightScale");
        private static readonly int CameraId = Shader.PropertyToID("_Camera");
        private static readonly int TileSizeId = Shader.PropertyToID("_TileSize");
        private static readonly int DensityId = Shader.PropertyToID("_Density");

        private readonly RowCropKernels _kernels;
        private readonly RowCropFields _fields;
        private readonly GraphicsBuffer _fieldBuffer;

        /// <summary>Uploads the fields and binds what stays fixed while the map is open.</summary>
        public RowCropKernelInputs(RowCropKernels kernels, RowCropFields fields, Terrain terrain)
        {
            _kernels = kernels;
            _fields = fields;
            _fieldBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, fields.fields.Count),
                RowCropField.Stride);
            _fieldBuffer.SetData(fields.fields);
            SetFields();
            SetHeights(terrain);
        }

        /// <summary>Whether the field asset's fields no longer fit the uploaded buffer.</summary>
        public bool Stale => _fieldBuffer.count != Mathf.Max(1, _fields.fields.Count);

        /// <summary>Whether a trample map is published, which changes every frame and so needs regrowing every frame.</summary>
        public static bool Trampled => Shader.GetGlobalVector(InteractionSurfaces.RectId).z > 0f;

        /// <summary>Binds one camera's view and the current trample map for its dispatches.</summary>
        public void SetCamera(Vector3 camera, float density, float distance)
        {
            var compute = _kernels.Compute;
            compute.SetVector(CameraId, new Vector4(camera.x, camera.y, camera.z, distance));
            compute.SetFloat(DensityId, density);
            var trample = Shader.GetGlobalTexture(InteractionSurfaces.TrampleId);
            var on = trample != null && Trampled;
            compute.SetTexture(_kernels.Plant, InteractionSurfaces.TrampleId, on ? trample : Texture2D.blackTexture);
            compute.SetVector(InteractionSurfaces.RectId, on ? Shader.GetGlobalVector(InteractionSurfaces.RectId) : Vector4.zero);
        }

        public void Dispose() => _fieldBuffer.Release();

        private void SetFields()
        {
            var compute = _kernels.Compute;
            var ids = _fields.fieldIds;
            var area = _fields.area;
            compute.SetBuffer(_kernels.Plant, FieldsId, _fieldBuffer);
            compute.SetTexture(_kernels.Plant, FieldIdsId, ids);
            compute.SetVector(FieldAreaId, new Vector4(area.xMin, area.yMin, ids.width / area.width, ids.height / area.height));
            compute.SetVector(FieldTexelsId, new Vector4(ids.width, ids.height, 0f, 0f));
            compute.SetFloat(TileSizeId, RowCropFields.TileSize);
        }

        /// <summary>The heightmap and how to read it: <c>_HeightScale</c> is base height, metres per unit, texels across.</summary>
        private void SetHeights(Terrain terrain)
        {
            var compute = _kernels.Compute;
            var data = terrain.terrainData;
            var origin = terrain.GetPosition();
            var size = data.size;
            compute.SetTexture(_kernels.Plant, HeightId, data.heightmapTexture);
            compute.SetVector(HeightAreaId, InteractionMath.Rect(new float2(origin.x, origin.z), new float2(size.x, size.z)));
            compute.SetVector(HeightScaleId, new Vector4(origin.y, size.y / HeightmapFull, data.heightmapResolution, 0f));
        }
    }
}
