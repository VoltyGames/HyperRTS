using System;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// Marks a string field that names a map library item by key, so editors offer a picker of the items of
    /// <see cref="Types"/> (library item classes such as <c>MapClimateSurface</c>; several when either fits).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class MapKeyAttribute : PropertyAttribute
    {
        public MapKeyAttribute(params Type[] types) => Types = types;

        public Type[] Types { get; }
    }
}
