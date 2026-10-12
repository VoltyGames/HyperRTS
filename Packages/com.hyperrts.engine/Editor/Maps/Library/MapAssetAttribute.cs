using System;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// Marks a string field that holds a project asset path, so editors show an object field of
    /// <see cref="Type"/> and write the picked asset's path back.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class MapAssetAttribute : PropertyAttribute
    {
        public MapAssetAttribute(Type type) => Type = type;

        public Type Type { get; }
    }
}
