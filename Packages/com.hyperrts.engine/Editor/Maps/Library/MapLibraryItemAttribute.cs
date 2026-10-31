using System;
using System.Reflection;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>Marks a map library item class with its type name: its assets are <c>&lt;Key&gt;.&lt;name&gt;.asset</c>.</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class MapLibraryItemAttribute : Attribute
    {
        public MapLibraryItemAttribute(string name) => Name = name;

        public string Name { get; }

        public static MapLibraryItemAttribute Require(Type type) => type.GetCustomAttribute<MapLibraryItemAttribute>()
            ?? throw new ArgumentException($"HyperRTS: {type.Name} is not a map library item class.", nameof(type));
    }
}
