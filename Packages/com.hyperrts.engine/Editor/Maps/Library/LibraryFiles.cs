using System;
using System.IO;
using UnityEditor;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>The Library page's file actions: new, duplicate and rename (move) item files, named <c>&lt;Key&gt;.&lt;type&gt;.json</c>.</summary>
    public static class LibraryFiles
    {
        /// <summary>The key a file at <paramref name="path"/> would define; throws when the name doesn't fit the type.</summary>
        public static string ParseKey(Type type, string path)
        {
            var suffix = $".{MapLibrary.TypeName(type)}.json";
            var name = Path.GetFileName(path);
            if (!name.EndsWith(suffix, StringComparison.Ordinal))
            {
                throw new ArgumentException($"'{name}' must be named <Key>{suffix}.");
            }

            var key = name[..^suffix.Length];
            if (key.Length == 0 || key.Contains('.'))
            {
                throw new ArgumentException($"'{name}' needs a key without dots before {suffix}.");
            }

            return key;
        }

        /// <summary>Writes an item of <paramref name="type"/> with its class defaults to <paramref name="path"/>.</summary>
        public static void Create(Type type, string path)
        {
            var (item, refs) = MapLibraryItemFile.Create(type);
            MapLibraryItemFile.Write(path, type, item, refs);
            AssetDatabase.ImportAsset(path);
        }

        public static void Copy(string source, string target)
        {
            if (!AssetDatabase.CopyAsset(source, target))
            {
                throw new InvalidOperationException($"HyperRTS: could not copy '{source}' to '{target}'.");
            }
        }

        public static void Move(string source, string target)
        {
            var error = AssetDatabase.MoveAsset(source, target);
            if (!string.IsNullOrEmpty(error))
            {
                throw new InvalidOperationException($"HyperRTS: could not move '{source}' to '{target}': {error}");
            }
        }
    }
}
