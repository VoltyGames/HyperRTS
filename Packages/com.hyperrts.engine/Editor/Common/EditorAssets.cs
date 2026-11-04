using HyperRTS.Simulation.Common.Authoring;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using HyperRTS.Simulation.GameEntities;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

[assembly: InternalsVisibleTo("HyperRTS.Editor.Maps")]
[assembly: InternalsVisibleTo("HyperRTS.Editor.Tests")]

namespace HyperRTS.Editor.Common
{
    /// <summary>Engine assets found by GUID or type, so tools keep working when the engine moves into a package.</summary>
    internal static partial class EditorAssets
    {
        private const string RigGuid = "4b013d5a34cf1e34d8d223e003462d25";

        private const string MatchStateGuid = "37a4b4ac2aee31d42895a37c3d702bed";

        [AutoStaticsCleanup]
        private static readonly Dictionary<string, Object> Found = new();

        /// <summary>
        /// The layout named after <paramref name="owner"/> plus <paramref name="part"/>, such as MapsPageRow.uxml, in
        /// the owner's package, or in Assets for a game type.
        /// </summary>
        public static VisualTreeAsset Layout(Type owner, string part = "") =>
            Find<VisualTreeAsset>(owner.Name + part, Folder(owner), owner.Name);

        /// <summary>The engine stylesheet called <paramref name="name"/>, for a control that brings its own styles.</summary>
        public static StyleSheet Sheet(string name) => Find<StyleSheet>(name, EnginePackage.Folder, name);

        private static T Find<T>(string name, string folder, string user) where T : Object
        {
            var key = $"{folder}/{name}.{typeof(T).Name}";
            if (Found.TryGetValue(key, out var cached) && cached != null)
            {
                return (T)cached;
            }

            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name} {name}", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                    Found[key] = asset;
                    return asset;
                }
            }

            throw new InvalidOperationException($"HyperRTS: {name} ({typeof(T).Name}) for {user} is missing from {folder}.");
        }

        private static string Folder(Type owner) =>
            UnityEditor.PackageManager.PackageInfo.FindForAssembly(owner.Assembly)?.assetPath ?? "Assets";

        public static GameObject RigPrefab => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(RigGuid));

        /// <summary>The ghost carrying MatchState; every map's SubScene holds one.</summary>
        public static GameObject MatchStatePrefab =>
            AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(MatchStateGuid));

        /// <summary>Every unit and building prefab in the project.</summary>
        public static List<GameEntityAuthoring> EntityPrefabs() => EntityPrefabs(AuthoringPrefabs());

        /// <summary>The unit and building prefabs among <paramref name="prefabs"/>.</summary>
        public static List<GameEntityAuthoring> EntityPrefabs(IEnumerable<GameObject> prefabs)
        {
            var result = new List<GameEntityAuthoring>();
            foreach (var prefab in prefabs)
            {
                if (prefab.TryGetComponent(out GameEntityAuthoring authoring))
                {
                    result.Add(authoring);
                }
            }

            return result;
        }

        /// <summary>Prefabs using any authoring script, engine or game; others are never loaded, so their missing scripts stay quiet.</summary>
        public static List<GameObject> AuthoringPrefabs()
        {
            var scripts = new HashSet<string>(MonoImporter.GetAllRuntimeMonoScripts()
                .Where(script => IsAuthoring(script.GetClass()))
                .Select(AssetDatabase.GetAssetPath));

            var result = new List<GameObject>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.GetDependencies(path, true).Any(scripts.Contains))
                {
                    result.Add(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                }
            }

            return result;
        }

        private static bool IsAuthoring(System.Type type) => type != null && typeof(AuthoringBehaviour).IsAssignableFrom(type);

        /// <summary>Selects an object and pings it in the Project or Hierarchy window.</summary>
        public static void Reveal(Object target)
        {
            UnityEditor.Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }

        /// <summary>The asset at <paramref name="path"/>, created empty there when missing, so rebuilds keep its GUID.</summary>
        public static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static List<T> FindAssets<T>() where T : Object
        {
            var result = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null)
                {
                    result.Add(asset);
                }
            }

            return result;
        }
    }
}
