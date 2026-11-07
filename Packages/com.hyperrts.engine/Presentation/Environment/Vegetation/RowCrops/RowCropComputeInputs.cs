using HyperRTS.Presentation.Environment.Ground.Stamps;
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

        /// <summary>Uploads the fields.</summary>
        public RowCropComputeInputs(ComputeShader compute, RowCropFields fields, Terrain terrain)
            : base(compute, "Plant", terrain, fields.fields.Count, RowCropField.Stride)
        {
            _fields = fields;
            _ids = fields.fieldIds;
            Items.SetData(fields.fields);
        }

        protected override int ItemCount => _fields.fields.Count;

        protected override bool OwnInputsStale => _ids != _fields.fieldIds;

        /// <summary>Binds the fields, their id map, the quality density and the trample map the crops bend under.</summary>
        protected override void BindCamera(ScatterSettings settings)
        {
            var area = _fields.area;
            Compute.SetBuffer(GrowKernel, FieldsId, Items);
            Compute.SetTexture(GrowKernel, FieldIdsId, _ids);
            Compute.SetVector(FieldAreaId,
                new Vector4(area.xMin, area.yMin, _ids.width / area.width, _ids.height / area.height));
            Compute.SetVector(FieldTexelsId, new Vector4(_ids.width, _ids.height, 0f, 0f));
            Compute.SetFloat(DensityId, settings.Density);
            TrampleSurfaces.TrampleGlobal.Bind(Compute, GrowKernel);
        }
    }
}
