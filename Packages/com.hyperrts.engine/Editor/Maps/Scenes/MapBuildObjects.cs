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

        public bool IsOwned(GameObject root)
        {
            if (root.name.StartsWith(MapScene.Generated, StringComparison.Ordinal))
            {
                return true;
            }

            return PrefabUtility.IsOutermostPrefabInstanceRoot(root) && _recorded.Contains(InstanceId(root));
        }

        /// <summary>Records a prefab instance this build made.</summary>
        public void Add(GameObject instance) => _made.Add(instance);

        /// <summary>Writes the record. Save the scene first, since unsaved objects have no ID yet.</summary>
        public void Save()
        {
            var ids = _made.Select(InstanceId).OrderBy(id => id, StringComparer.Ordinal);
            File.WriteAllLines(_file, ids);
            AssetDatabase.ImportAsset(_file);
        }

        private static string InstanceId(GameObject root)
        {
            Object instance = PrefabUtility.GetPrefabInstanceHandle(root)
                ?? throw new ArgumentException($"HyperRTS: '{root.name}' is not a prefab instance.", nameof(root));
            return GlobalObjectId.GetGlobalObjectIdSlow(instance).ToString();
        }
    }
}
