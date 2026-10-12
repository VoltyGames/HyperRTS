using System;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// A file that names a library item: a library item (<see cref="Type"/> and <see cref="Key"/> set) or a map
    /// spec (both null), and the field that names it.
    /// </summary>
    public readonly struct MapLibraryUsage
    {
        public readonly string File;
        public readonly Type Type;
        public readonly string Key;
        public readonly string Field;

        public MapLibraryUsage(string file, Type type, string key, string field)
        {
            File = file;
            Type = type;
            Key = key;
            Field = field;
        }

        public override string ToString() => Type == null ? $"{File} ({Field})" : $"{MapLibrary.TypeName(Type)} {Key} ({Field})";
    }
}
