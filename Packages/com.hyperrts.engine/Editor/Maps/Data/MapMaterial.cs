using System;
using HyperRTS.Editor.Maps.Library;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>
    /// A shared material map props are built with (model slots named after it are remapped onto it), usually made
    /// by its factory from the photo texture set beside its file.
    /// </summary>
    [Serializable]
    [MapLibraryItem("material", "material")]
    public sealed class MapMaterial : IMapKeyed, IMapLibraryItem
    {
        [NonSerialized]
        public string key;

        [NonSerialized]
        public Material material;

        string IMapKeyed.Key => key;

        void IMapLibraryItem.Load(string key, MapLibraryRefs refs)
        {
            this.key = key;
            material = refs.Asset<Material>(refs.material, "material");
        }
    }
}
