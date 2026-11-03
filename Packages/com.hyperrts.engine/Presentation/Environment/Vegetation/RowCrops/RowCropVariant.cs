using System;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Vegetation.RowCrops
{
    /// <summary>One model of a <see cref="RowCrop"/>: its full and simple plant meshes, each with one submesh.</summary>
    [Serializable]
    public sealed class RowCropVariant
    {
        [Tooltip("Full plant mesh, origin at the root, one submesh.")]
        public Mesh lod0;

        [Tooltip("Simpler plant mesh past lod0Distance, one submesh.")]
        public Mesh lod1;

        /// <summary>The mesh drawn in a <see cref="RowCropBands"/> band.</summary>
        public Mesh Mesh(int lod) => lod switch
        {
            RowCropBands.Full => lod0,
            RowCropBands.Simple => lod1,
            _ => throw new ArgumentOutOfRangeException(nameof(lod), lod, "Row crop bands are 0 and 1."),
        };
    }
}
