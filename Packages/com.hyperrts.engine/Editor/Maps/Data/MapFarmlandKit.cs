using HyperRTS.Core;
using HyperRTS.Editor.Maps.Library;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>
    /// How a climate farms its land: field sizes on the grid, the crop mix, weedy margins, shelterbelts across the
    /// field ends and the dirt tracks beside them.
    /// </summary>
    [MapLibraryItem("farmland")]
    [CreateAssetMenu(menuName = HyperRTSMenu.MapLibrary + "Farmland Kit", fileName = "New.farmland")]
    public sealed class MapFarmlandKit : MapLibraryItem
    {
        [Tooltip("Field length along the rows (x) and width across them (y), in metres.")]
        public Vector2 fieldSize = new(160f, 80f);

        public List<MapCropShare> crops = new();

        [Tooltip("Weedy strip round each field (metres).")]
        public float marginWidth = 2.5f;

        [MapKey(typeof(MapClimateSurface))]

        public string marginSurface;

        [Tooltip("Belt kit planted across the field ends; empty for none.")]
        [MapKey(typeof(MapBeltKit))]
        public string beltKit;

        [Tooltip("A belt on every Nth line of field ends.")]
        [Min(1)]
        public int beltEvery = 1;

        [Tooltip("Road class of the tracks along the field ends; empty for none.")]
        [MapKey(typeof(MapRoadClass))]
        public string trackClass;

        [Tooltip("A field-end track ending this close to a road's edge (metres) is carried onto the road.")]
        public float trackJoinDistance;

        [Tooltip("Metres past the margin at each field end where the rows turn; 0 for none.")]
        public float headlandWidth;

        [Tooltip("Field fragments smaller than this (m²) become leftover ground.")]
        public float minField = 1500f;

        [Tooltip("Field fragments narrower than this (m) at their widest become leftover ground: no strip between a ravine and a track.")]
        public float minFieldWidth = 20f;

        [Tooltip("Surfaces for the pieces too small or narrow to farm (fallow, pasture), one picked per field; empty leaves them margin.")]
        public List<MapCropShare> leftovers = new();
    }
}
