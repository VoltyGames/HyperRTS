using System;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// Marks a <see cref="MapAssetRef"/> field with the asset <see cref="Type"/> it must link to, so checks and
    /// editors know what to load.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class MapAssetAttribute : PropertyAttribute
    {
        public MapAssetAttribute(Type type) => Type = type;

        public Type Type { get; }
    }
}
