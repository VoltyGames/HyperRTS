using System;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.RowCrops
{
    /// <summary>One model of a <see cref="RowCrop"/>: its plant meshes and card, each with one submesh.</summary>
    [Serializable]
    public sealed class RowCropVariant
    {
        [Tooltip("Full plant mesh, origin at the root, one submesh.")]
        public Mesh lod0;

        [Tooltip("Simpler plant mesh past lod0Distance, one submesh.")]
        public Mesh lod1;

        [Tooltip("Impostor card past lod1Distance, one submesh.")]
        public Mesh card;

        /// <summary>The mesh drawn in a <see cref="RowCropView"/> band.</summary>
        public Mesh Mesh(int lod) => lod switch
        {
            RowCropView.Full => lod0,
            RowCropView.Simple => lod1,
            RowCropView.Card => card,
            _ => throw new ArgumentOutOfRangeException(nameof(lod), lod, "Row crop bands are 0 to 2."),
        };
    }
}
