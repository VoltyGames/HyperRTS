using System.Collections.Generic;
using System.Linq;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Common
{
    /// <summary>Engine assets found by GUID or type, so tools keep working when the engine moves into a package.</summary>
    internal static class EditorAssets
    {
        private const string RigGuid = "4b013d5a34cf1e34d8d223e003462d25";

        public static GameObject RigPrefab => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(RigGuid));

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
