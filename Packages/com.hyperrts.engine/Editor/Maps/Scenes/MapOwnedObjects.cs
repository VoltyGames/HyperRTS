using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Library;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Scenes
{
    /// <summary>
    /// The SubScene roots a build owns and clears: those it named <c>[Generated]</c>, and every instance of a library
    /// prefab at the root, because a prefab whose internal IDs changed drops the instance's name override.
    /// Hand-placed library prefabs belong under <c>[Dressing]</c>.
    /// </summary>
    public static class MapOwnedObjects
    {
        /// <summary>The asset paths of every prefab a library item can place.</summary>
        public static HashSet<string> LibraryPrefabs(MapLibrary library)
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var type in MapLibrary.ItemTypes)
            {
                foreach (var key in library.Keys(type))
                {
                    var (_, refs) = MapLibraryItemFile.Read(type, library.FilePath(type, key));
                    AddPath(paths, refs.prefab);
                    refs.prefabs.ForEach(path => AddPath(paths, path));
                    refs.destructibles.ForEach(path => AddPath(paths, path));
                }
            }

            return paths;
        }

        public static bool IsOwned(GameObject root, ISet<string> libraryPrefabs)
        {
            if (root.name.StartsWith(MapScene.Generated, StringComparison.Ordinal))
            {
                return true;
            }

            if (!PrefabUtility.IsOutermostPrefabInstanceRoot(root))
            {
                return false;
            }

            return libraryPrefabs.Contains(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root));
        }

        private static void AddPath(HashSet<string> paths, string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                paths.Add(path);
            }
        }
    }
}
