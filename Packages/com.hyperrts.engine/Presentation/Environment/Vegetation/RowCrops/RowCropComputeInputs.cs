using HyperRTS.Presentation.Environment.Trampling;
using HyperRTS.Presentation.Environment.Vegetation.Scatter;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Vegetation.RowCrops
{
    /// <summary>What RowCrops.compute reads beside the shared inputs: the fields, their id map and the trample map. The canopy reads the fields too.</summary>
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

        // x min, z min, texels per metre in x and z.
        private Vector4 FieldArea =>
            new(_fields.area.xMin, _fields.area.yMin, _ids.width / _fields.area.width, _ids.height / _fields.area.height);

        private Vector4 FieldTexels => new(_ids.width, _ids.height, 0f, 0f);

        /// <summary>Binds the fields and their id map for HyperRTS/RowCropCanopy, which finds each pixel's field as the kernel does.</summary>
        public void Bind(MaterialPropertyBlock block)
        {
            block.SetBuffer(FieldsId, Items);
            block.SetTexture(FieldIdsId, _ids);
            block.SetVector(FieldAreaId, FieldArea);
            block.SetVector(FieldTexelsId, FieldTexels);
        }

        protected override int ItemCount => _fields.fields.Count;

        protected override bool OwnInputsStale => _ids != _fields.fieldIds;

        /// <summary>Binds the fields, their id map, the quality density and the trample map the crops bend under.</summary>
        protected override void BindCamera(ScatterSettings settings)
        {
            Compute.SetBuffer(GrowKernel, FieldsId, Items);
            Compute.SetTexture(GrowKernel, FieldIdsId, _ids);
            Compute.SetVector(FieldAreaId, FieldArea);
            Compute.SetVector(FieldTexelsId, FieldTexels);
            Compute.SetFloat(DensityId, settings.Density);
            TrampleSurfaces.TrampleGlobal.Bind(Compute, GrowKernel);
        }
    }
}
