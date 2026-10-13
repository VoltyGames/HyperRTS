using System;
using System.Collections.Generic;
using System.Reflection;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// Marks a map library item class: its type name (its files are <c>&lt;Key&gt;.&lt;name&gt;.json</c>) and the
    /// <see cref="MapLibraryRefs"/> fields it reads, in the order editors show and write them ahead of its own fields.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class MapLibraryItemAttribute : Attribute
    {
        public MapLibraryItemAttribute(string name, params string[] refs)
        {
            Name = name;
            Refs = refs;
        }

        public string Name { get; }

        public IReadOnlyList<string> Refs { get; }

        /// <summary>The asset refs that picture the item in lists, first set one wins; empty for no picture.</summary>
        public string[] PreviewRefs { get; set; } = Array.Empty<string>();

        public static MapLibraryItemAttribute Require(Type type) => type.GetCustomAttribute<MapLibraryItemAttribute>()
            ?? throw new ArgumentException($"HyperRTS: {type.Name} is not a map library item class.", nameof(type));
    }
}
