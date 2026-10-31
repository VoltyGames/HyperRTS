using HyperRTS.Core;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Presentation.Environment.RowCrops;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>A crop grown in 3D rows on the fields of the surfaces that name it.</summary>
    [MapLibraryItem("crop")]
    [CreateAssetMenu(menuName = HyperRTSMenu.MapLibrary + "Row Crop", fileName = "New.crop")]
    public sealed class MapClimateRowCrop : MapLibraryItem
    {
        [Tooltip("The crop's meshes, materials and planting.")]
        public RowCrop crop;
    }
}
