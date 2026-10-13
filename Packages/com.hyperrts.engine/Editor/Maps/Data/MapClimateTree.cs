using System;
using HyperRTS.Editor.Maps.Library;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>A tree the climate grows: a terrain tree, and optionally an entity version for the playable area.</summary>
    [Serializable]
    [MapLibraryItem("tree", "prefab", "destructible", PreviewRefs = new[] { "prefab" })]
    public sealed class MapClimateTree : IMapKeyed, IMapLibraryItem
    {
        [NonSerialized]
        public string key;

        [Tooltip("Mesh prefab, ideally with an LOD Group; origin at the base of the trunk.")]
        [NonSerialized]
        public GameObject prefab;

        [Tooltip("Entity version used inside the playable area, so trees there can be shot down and crushed " +
                 "(Destructible authoring); empty keeps terrain trees everywhere.")]
        [NonSerialized]
        public GameObject destructible;

        [Tooltip("Random uniform scale range.")]
        public Vector2 scale = new(0.85f, 1.15f);

        string IMapKeyed.Key => key;

        void IMapLibraryItem.Load(string key, MapLibraryRefs refs)
        {
            this.key = key;
            prefab = refs.Asset<GameObject>(refs.prefab, "prefab");
            destructible = refs.Asset<GameObject>(refs.destructible, "destructible");
        }
    }
}
