using System;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Presentation.Environment.RowCrops;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>A crop grown in 3D rows on the fields of the surfaces that name it.</summary>
    [Serializable]
    [MapLibraryItem("rowCrop", "crop")]
    public sealed class MapClimateRowCrop : IMapKeyed, IMapLibraryItem
    {
        [NonSerialized]
        public string key;

        [Tooltip("The crop's meshes, materials and sowing.")]
        [NonSerialized]
        public RowCrop crop;

        string IMapKeyed.Key => key;

        void IMapLibraryItem.Load(string key, MapLibraryRefs refs)
        {
            this.key = key;
            crop = refs.Asset<RowCrop>(refs.crop, "crop");
        }
    }
}
