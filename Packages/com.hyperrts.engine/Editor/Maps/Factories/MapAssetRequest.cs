using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HyperRTS.Editor.Maps.Library;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Factories
{
    /// <summary>One library item handed to its <see cref="IMapAssetFactory"/>: its key, file, asset paths and params.</summary>
    public sealed class MapAssetRequest
    {
        public MapAssetRequest(MapLibrary library, Type type, string key, string file)
        {
            Library = library;
            Type = type;
            Key = key;
            File = file;
            Json = System.IO.File.ReadAllText(file);
            (_, Refs) = MapLibraryItemFile.Parse(type, Json, file);
        }

        /// <summary>The library the item belongs to, for factories that use other items (a prop's materials).</summary>
        public MapLibrary Library { get; }

        public Type Type { get; }

        public string Key { get; }

        /// <summary>The item's file (project-relative), named in every error.</summary>
        public string File { get; }

        /// <summary>The folder holding the item's file, where texture sets beside it are found.</summary>
        public string Folder => Path.GetDirectoryName(File).Replace('\\', '/');

        public string Json { get; }

        /// <summary>The asset paths the item names, which are where its factory writes.</summary>
        public MapLibraryRefs Refs { get; }

        /// <summary>
        /// The <c>params</c> object read into <typeparamref name="T"/>. Every serialized field of <typeparamref name="T"/>
        /// must be given and no other key may appear, so a typo or a missing value fails naming the key.
        /// </summary>
        public T Params<T>() where T : new()
        {
            if (Refs.factoryParams == null)
            {
                throw Error($"has no \"params\" object for factory '{Refs.factory}'");
            }

            var given = MapJsonMembers.Of(Refs.factoryParams).Select(member => member.Name).ToList();
            var missing = ParamFields<T>.Names.Except(given).ToList();
            var unknown = given.Except(ParamFields<T>.Names).ToList();
            if (missing.Count > 0 || unknown.Count > 0)
            {
                throw Error($"params for factory '{Refs.factory}' miss [{string.Join(", ", missing)}] " +
                            $"and have unknown [{string.Join(", ", unknown)}]");
            }

            var values = new T();
            JsonUtility.FromJsonOverwrite(Refs.factoryParams, values);
            return values;
        }

        /// <summary>The asset path an item field names, which the factory must write; empty is an error.</summary>
        public string Output(string path, string field) =>
            string.IsNullOrEmpty(path) ? throw Error($"leaves '{field}' empty, so factory '{Refs.factory}' has nowhere to write") : path;

        /// <summary>The asset at a params path; a missing or wrong-typed asset is an error naming <paramref name="field"/>.</summary>
        public T Asset<T>(string path, string field) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw Error($"has params.{field} '{path}', which is not a {typeof(T).Name} asset");

        /// <summary>An error naming the item's file.</summary>
        public InvalidOperationException Error(string problem) =>
            new($"HyperRTS: map library file '{File}' {problem}.");

        /// <summary>The serialized field names of a params class, read once per class.</summary>
        private static class ParamFields<T>
        {
            public static readonly string[] Names = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(field => !field.IsNotSerialized)
                .Select(field => field.Name)
                .ToArray();
        }
    }
}
