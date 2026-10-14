using System;
using HyperRTS.Core;
using UnityEngine;

namespace HyperRTS.Simulation.Effects
{
    /// <summary>A rig per <see cref="SurfaceMaterial"/>: what a shot throws up where it lands, or wheels as they roll.</summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Effects + "Surface Effects", fileName = "SurfaceEffects")]
    [HelpURL(HyperRTSDocs.Effects)]
    public class SurfaceEffects : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public SurfaceMaterial material;
            public GameObject rig;
        }

        [Tooltip("Rig for every material without an entry of its own.")]
        public GameObject anyMaterial;

        [Tooltip("Rigs for particular materials (water splash, metal sparks, masonry chips).")]
        public Entry[] materials = { };

        /// <summary>The material's rig, else <see cref="anyMaterial"/>.</summary>
        public GameObject RigFor(SurfaceMaterial material)
        {
            foreach (var entry in materials)
            {
                if (entry.material == material)
                {
                    return entry.rig;
                }
            }

            return anyMaterial;
        }
    }
}
