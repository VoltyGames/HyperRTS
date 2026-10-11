using System;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>A kind of road spec roads and generated tracks name: its width, ground, mesh and verge.</summary>
    [Serializable]
    public sealed class MapRoadClass
    {
        public string key;

        [Tooltip("Default width (metres); a spec road can override it.")]
        public float width = 6f;

        [Tooltip("Surface painted under the road and its soft edges.")]
        public string surface;

        [Tooltip("Material of the road mesh laid over the terrain (HyperRTS/Road); empty paints the terrain only.")]
        public Material material;

        [Tooltip("Metres of road per texture repeat along the mesh.")]
        public float tileLength = 10f;

        [Tooltip("Strip of grass and weeds either side of the road (metres).")]
        public float vergeWidth = 3f;

        public string vergeSurface;

        [Tooltip("At a junction the lower-ranked road ends under the higher-ranked one.")]
        public int rank;

        [Tooltip("Levelled onto a smoothed profile (on embankments over ravines); off, it follows the ground.")]
        public bool level = true;

        [Tooltip("Corner radius the builder rounds the authored polyline to (metres).")]
        public float curve = 25f;
    }
}
