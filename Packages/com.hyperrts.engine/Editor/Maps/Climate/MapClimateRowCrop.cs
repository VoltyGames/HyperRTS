using HyperRTS.Core;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Presentation.Environment.Vegetation.RowCrops;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Climate
{
    /// <summary>A crop grown in 3D rows on the fields of the surfaces that name it.</summary>
    [MapLibraryItem("crop")]
    [CreateAssetMenu(menuName = HyperRTSMenu.MapLibrary + "Row Crop", fileName = "New.crop")]
    public sealed class MapClimateRowCrop : MapLibraryItem
    {
        [Tooltip("The crop's meshes, materials and planting.")]
        public RowCrop crop;

        [Tooltip("Share of sight lost per metre through the grown crop below its height: 0.05 hides what lies past 20 m. 0 for crops lower than an eye.")]
        [Min(0f)]
        public float sightDensity;
    }
}
