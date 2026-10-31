using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Validation
{
    /// <summary>
    /// The project's assets for every <see cref="Rules.IAssetRule"/>: each type and folder is loaded once, however
    /// many rules ask for it, and kept until the next import.
    /// </summary>
    public sealed class AssetSet
    {
        [AutoStaticsCleanup]
        private static AssetSet _project;

        private readonly Func<Type, string[], IEnumerable<Object>> _load;
        private readonly Dictionary<string, object> _loaded = new();

        private AssetSet(Func<Type, string[], IEnumerable<Object>> load) => _load = load;

        public static AssetSet InProject() => _project ??= new AssetSet(Load);

        /// <summary>A set of the given assets only, such as ones a test made in memory; folders don't filter it.</summary>
        public static AssetSet From(params Object[] assets) => new((type, _) => assets.Where(type.IsInstanceOfType));

        /// <summary>Every asset of type <typeparamref name="T"/> under <paramref name="folders"/>, or under Assets when none are given.</summary>
        public IReadOnlyList<T> All<T>(params string[] folders) where T : Object
        {
            var key = typeof(T).FullName + "|" + string.Join("|", folders);
            if (!_loaded.TryGetValue(key, out var found))
            {
                found = _load(typeof(T), folders.Length > 0 ? folders : new[] { "Assets" }).Cast<T>().ToList();
                _loaded[key] = found;
            }

            return (IReadOnlyList<T>)found;
        }

        internal static void Forget() => _project = null;

        private static IEnumerable<Object> Load(Type type, string[] folders) =>
            AssetDatabase.FindAssets("t:" + type.Name, folders)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .Select(path => AssetDatabase.LoadAssetAtPath(path, type))
                .Where(asset => asset != null);
    }
}
