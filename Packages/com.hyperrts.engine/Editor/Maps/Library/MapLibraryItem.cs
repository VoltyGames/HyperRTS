using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// A map library item, one asset named <c>&lt;Key&gt;.&lt;type&gt;.asset</c> (<c>Stubble.surface.asset</c> is the
    /// surface <c>Stubble</c>). <see cref="MapLibrary"/> hands out a copy per load, with its key and asset path set, so
    /// a build can fill the copy's derived fields without touching the asset.
    /// </summary>
    public abstract class MapLibraryItem : ScriptableObject, IMapKeyed
    {
        /// <summary>The key specs and other items name it by: the asset name before its type suffix.</summary>
        [NonSerialized]
        public string key;

        /// <summary>The asset it was loaded from, named in errors; null for an item made in memory.</summary>
        [NonSerialized]
        public string file;

        string IMapKeyed.Key => key;

        /// <summary>The key of an item asset named <paramref name="assetName"/>: everything before the first dot.</summary>
        public static string ParseKey(string assetName)
        {
            var dot = assetName.IndexOf('.');
            return dot < 0 ? assetName : assetName[..dot];
        }

        /// <summary>Fills what the item derives from its fields once its copy is made; throws on data a build can't use.</summary>
        protected internal virtual void Load()
        {
        }

        /// <summary>Throws naming the first empty entry of <paramref name="assets"/>, which a build can't place.</summary>
        protected void RequireFilled<T>(List<T> assets, string field) where T : Object
        {
            var empty = assets.FindIndex(asset => asset == null);
            if (empty >= 0)
            {
                throw new InvalidOperationException($"HyperRTS: map library asset '{file ?? key}': {field}[{empty}] is empty.");
            }
        }
    }
}
