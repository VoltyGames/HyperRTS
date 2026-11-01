using HyperRTS.Presentation.Environment.Trampling;
using HyperRTS.Presentation.Environment.Vegetation.Scatter;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Vegetation.RowCrops
{
    /// <summary>What RowCrops.compute reads beside the shared inputs: the fields, their id map and the trample map.</summary>
    internal sealed class RowCropComputeInputs : ScatterComputeInputs
    {
        private static readonly int FieldsId = Shader.PropertyToID("_Fields");
        private static readonly int FieldIdsId = Shader.PropertyToID("_FieldIds");
        private static readonly int FieldAreaId = Shader.PropertyToID("_FieldArea");
        private static readonly int FieldTexelsId = Shader.PropertyToID("_FieldTexels");
        private static readonly int DensityId = Shader.PropertyToID("_Density");

        private readonly RowCropFields _fields;
        private readonly Texture _ids;

        /// <summary>Uploads the fields and binds what stays fixed while the map is open.</summary>
        public RowCropComputeInputs(ComputeShader compute, RowCropFields fields, Terrain terrain)
            : base(compute, "Plant", terrain, fields.fields.Count, RowCropField.Stride)
        {
            _fields = fields;
            Items.SetData(fields.fields);
            var ids = _ids = fields.fieldIds;
            var area = fields.area;
            compute.SetBuffer(GrowKernel, FieldsId, Items);
            compute.SetTexture(GrowKernel, FieldIdsId, ids);
            compute.SetVector(FieldAreaId, new Vector4(area.xMin, area.yMin, ids.width / area.width, ids.height / area.height));
            compute.SetVector(FieldTexelsId, new Vector4(ids.width, ids.height, 0f, 0f));
        }

        protected override int ItemCount => _fields.fields.Count;

        protected override bool OwnInputsStale => _ids != _fields.fieldIds;

        /// <summary>Binds the quality density and the trample map the crops bend under.</summary>
        protected override void BindCamera(ScatterSettings settings)
        {
            Compute.SetFloat(DensityId, settings.Density);
            TrampleSurfaces.TrampleGlobal.Bind(Compute, GrowKernel);
        }
    }
}
