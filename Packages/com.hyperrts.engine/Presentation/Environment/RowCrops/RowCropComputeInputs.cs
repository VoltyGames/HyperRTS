using System;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.RowCrops
{
    /// <summary>
    /// What RowCrops.compute reads. The trample map is copied from the globals, as the shared inputs copy the rest:
    /// compute shaders don't see them.
    /// </summary>
    internal sealed class RowCropComputeInputs : IDisposable
    {
        private static readonly int FieldsId = Shader.PropertyToID("_Fields");
        private static readonly int FieldIdsId = Shader.PropertyToID("_FieldIds");
        private static readonly int FieldAreaId = Shader.PropertyToID("_FieldArea");
        private static readonly int FieldTexelsId = Shader.PropertyToID("_FieldTexels");
        private static readonly int DensityId = Shader.PropertyToID("_Density");

        private readonly RowCropCompute _kernels;
        private readonly RowCropFields _fields;
        private readonly GraphicsBuffer _fieldBuffer;
        private readonly ScatterInputs _scatter;
        private Texture _ids;

        /// <summary>Uploads the fields and binds what stays fixed while the map is open.</summary>
        public RowCropComputeInputs(RowCropCompute kernels, RowCropFields fields, Terrain terrain)
        {
            _kernels = kernels;
            _fields = fields;
            _fieldBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, fields.fields.Count),
                RowCropField.Stride);
            _fieldBuffer.SetData(fields.fields);
            SetFields();
            _scatter = new ScatterInputs(kernels.Compute, kernels.Plant, terrain);
        }

        /// <summary>
        /// Whether the field asset's fields no longer fit the uploaded buffer, or a map rebuild replaced the field ids or
        /// the heightmap bound to the kernel.
        /// </summary>
        public bool Stale
        {
            get
            {
                if (_fieldBuffer.count != Mathf.Max(1, _fields.fields.Count))
                {
                    return true;
                }

                return _ids != _fields.fieldIds || _scatter.Stale;
            }
        }

        /// <summary>Binds one camera's view, the quality density and the trample map for its dispatches.</summary>
        public void SetCamera(Vector3 camera, ScatterSettings settings)
        {
            _kernels.Compute.SetFloat(DensityId, settings.Density);
            _scatter.BindCamera(camera, settings.Distance);
            InteractionSurfaces.BindTrample(_kernels.Compute, _kernels.Plant);
        }

        public void Dispose() => _fieldBuffer.Release();

        private void SetFields()
        {
            var compute = _kernels.Compute;
            var ids = _ids = _fields.fieldIds;
            var area = _fields.area;
            compute.SetBuffer(_kernels.Plant, FieldsId, _fieldBuffer);
            compute.SetTexture(_kernels.Plant, FieldIdsId, ids);
            compute.SetVector(FieldAreaId, new Vector4(area.xMin, area.yMin, ids.width / area.width, ids.height / area.height));
            compute.SetVector(FieldTexelsId, new Vector4(ids.width, ids.height, 0f, 0f));
        }
    }
}
