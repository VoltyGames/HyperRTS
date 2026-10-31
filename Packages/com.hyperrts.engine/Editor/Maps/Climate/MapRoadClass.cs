using System;
using HyperRTS.Core;
using HyperRTS.Editor.Maps.Library;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Climate
{
    /// <summary>A kind of road spec roads and generated tracks name: its width, ground, mesh and verge.</summary>
    [MapLibraryItem("road")]
    [CreateAssetMenu(menuName = HyperRTSMenu.MapLibrary + "Road Class", fileName = "New.road")]
    public sealed class MapRoadClass : MapLibraryItem
    {
        [Tooltip("Default width (metres); a spec road can override it.")]
        public float width = 6f;

        [Tooltip("Surface painted under the road and its soft edges.")]
        [MapKey(typeof(MapClimateSurface))]
        public string surface;

        [Tooltip("SurfaceMaterial name of the road itself, which its painted surface may not match (Paved, Gravel, Soil...).")]
        public string impactClass;

        [Tooltip("Material of the road mesh laid over the terrain (HyperRTS/Road); empty paints the terrain only.")]
        public Material material;

        [Tooltip("Metres of road per texture repeat along the mesh.")]
        public float tileLength = 10f;

        [Tooltip("Strip of grass and weeds either side of the road (metres).")]
        public float vergeWidth = 3f;

        [MapKey(typeof(MapClimateSurface))]
        public string vergeSurface;

        [Tooltip("At a junction the lower-ranked road ends under the higher-ranked one.")]
        public int rank;

        [Tooltip("Levelled onto a smoothed profile (on embankments over ravines); off, it follows the ground.")]
        public bool level = true;

        [Tooltip("Corner radius the builder rounds the authored polyline to (metres).")]
        public float cornerRadius = 25f;
    }
}
