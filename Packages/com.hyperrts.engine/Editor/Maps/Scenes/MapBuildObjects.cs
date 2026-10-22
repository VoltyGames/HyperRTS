using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Maps.Scenes
{
    /// <summary>
    /// The SubScene roots a build owns: those named <c>[Generated]</c>, and the prefab instances it recorded in
    /// <c>&lt;Map&gt;_BuildObjects.txt</c>. An instance is recorded by its PrefabInstance, whose ID survives the
    /// prefab's internal IDs changing, which drops the instance's name override. Everything else stays.
    /// </summary>
    public sealed class MapBuildObjects
    {
        private readonly string _file;
        private readonly HashSet<string> _recorded;
        private readonly List<GameObject> _made = new();

        /// <summary>Reads the last build's record at <paramref name="file"/>. A map never built has none.</summary>
        public MapBuildObjects(string file)
        {
            _file = file;
            var lines = File.Exists(file) ? File.ReadAllLines(file) : Array.Empty<string>();
            _recorded = new HashSet<string>(lines, StringComparer.Ordinal);
        }

        /// <summary>The roots among <paramref name="roots"/> this build owns, in their order.</summary>
        public List<GameObject> Owned(IReadOnlyList<GameObject> roots)
        {
            var instances = roots.Where(root => !IsGenerated(root) && PrefabUtility.IsOutermostPrefabInstanceRoot(root))
                .ToList();
            var ids = InstanceIds(instances);
            var recorded = new HashSet<GameObject>(instances.Where((_, i) => _recorded.Contains(ids[i])));
            return roots.Where(root => IsGenerated(root) || recorded.Contains(root)).ToList();
        }

        /// <summary>Records a prefab instance this build made.</summary>
        public void Add(GameObject instance) => _made.Add(instance);

        /// <summary>Writes the record. Save the scene first, since unsaved objects have no ID yet.</summary>
        public void Save()
        {
            var ids = InstanceIds(_made).OrderBy(id => id, StringComparer.Ordinal);
            File.WriteAllLines(_file, ids);
            AssetDatabase.ImportAsset(_file);
        }

        private static bool IsGenerated(GameObject root) => root.name.StartsWith(MapScene.Generated, StringComparison.Ordinal);

        // Batched, since each GetGlobalObjectIdSlow call is slow on its own.
        private static string[] InstanceIds(IReadOnlyList<GameObject> roots)
        {
            var instances = roots.Select(Handle).ToArray();
            var ids = new GlobalObjectId[instances.Length];
            GlobalObjectId.GetGlobalObjectIdsSlow(instances, ids);
            return ids.Select(id => id.ToString()).ToArray();
        }

        private static Object Handle(GameObject root) => PrefabUtility.GetPrefabInstanceHandle(root)
            ?? throw new ArgumentException($"HyperRTS: '{root.name}' is not a prefab instance.", nameof(root));
    }
}
