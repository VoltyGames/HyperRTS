using System;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Resources;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>One placeable prefab.</summary>
    [Serializable]
    public sealed class MapPaletteEntry : IMapKeyed, IMapLibraryItem
    {
        [NonSerialized]
        public string key;

        [NonSerialized]
        public GameObject prefab;

        [Tooltip("Footprint (width across, depth along the prefab's forward) in metres; spacing and levelling use it.")]
        public Vector2 size = new(4f, 4f);

        [Tooltip("Level the ground under the footprint before placing.")]
        public bool flatten;

        [Tooltip("Random yaw added on top of the spec's (degrees, either way).")]
        public float yawJitter;

        [Tooltip("Random uniform scale range.")]
        public Vector2 scale = Vector2.one;

        [Tooltip("Climate surface painted under and around the footprint (a gravel pad); empty for none.")]
        public string pad;

        [Tooltip("How far the pad reaches past the footprint (metres).")]
        public float padMargin = 3f;

        [Tooltip("Road class of an access track run from the pad to the nearest road; empty for none.")]
        public string access;

        public float Radius => Mathf.Max(size.x, size.y) * 0.5f;

        /// <summary>Prefabs with HyperRTS authoring bake into the SubScene; the rest are scenery in the main scene.</summary>
        public bool IsEntity => prefab.GetComponentInChildren<AuthoringBehaviour>(true) != null;

        /// <summary>A resource node. Looked up on the live prefab each time, so edits to it show on the next build.</summary>
        public bool IsResource => prefab.GetComponentInChildren<ResourceNodeAuthoring>() != null;

        /// <summary>Something players use (a building, unit or resource node), as opposed to scenery.</summary>
        public bool IsGameplay =>
            prefab.GetComponentInChildren<GameEntityAuthoring>(true) != null
            || prefab.GetComponentInChildren<ResourceNodeAuthoring>(true) != null;

        string IMapKeyed.Key => key;

        void IMapLibraryItem.Load(string key, MapLibraryRefs refs)
        {
            this.key = key;
            prefab = refs.Asset<GameObject>(refs.prefab, "prefab");
        }
    }
}
