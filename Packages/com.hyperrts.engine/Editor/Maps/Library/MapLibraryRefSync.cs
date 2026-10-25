using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// Keeps every <see cref="MapAssetRef"/> in step: a saved library file gets the GUIDs of the paths it names, and a
    /// moved asset's new path is written into the files that link it.
    /// </summary>
    public sealed class MapLibraryRefSync : AssetPostprocessor
    {
        [NoAutoStaticsCleanup]
        private static readonly HashSet<string> Pending = new(StringComparer.Ordinal);
        [NoAutoStaticsCleanup]
        private static bool _queued;

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            Pending.UnionWith(imported.Concat(moved).Where(IsLibraryFile));
            var movedGuids = moved.Where(path => !IsLibraryFile(path)).Select(path => AssetDatabase.AssetPathToGUID(path)).ToList();
            if (movedGuids.Count > 0)
            {
                Pending.UnionWith(LinkingFiles(movedGuids));
            }

            if (Pending.Count == 0 || _queued)
            {
                return;
            }

            // After the import, so the moved assets resolve at their new paths.
            _queued = true;
            EditorApplication.delayCall += SyncPending;
        }

        private static bool IsLibraryFile(string path) => MapLibrary.ParseFile(path).Type != null;

        /// <summary>The library files whose text names one of <paramref name="guids"/>.</summary>
        private static IEnumerable<string> LinkingFiles(IReadOnlyList<string> guids)
        {
            var library = MapLibrary.Load();
            var files = MapLibrary.ItemTypes.SelectMany(type => library.Keys(type).Select(key => library.FilePath(type, key)));
            return files.Where(file => NamesAny(File.ReadAllText(file), guids)).ToList();
        }

        private static bool NamesAny(string text, IReadOnlyList<string> guids) =>
            guids.Any(guid => text.Contains(guid, StringComparison.Ordinal));

        private static void SyncPending()
        {
            _queued = false;
            var files = Pending.ToList();
            Pending.Clear();
            var changed = files.Where(Sync).ToList();
            if (changed.Count == 0)
            {
                return;
            }

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var file in changed)
                {
                    AssetDatabase.ImportAsset(file);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        /// <summary>Rewrites <paramref name="file"/> when a link changed. Unreadable files are left for <see cref="MapLibraryProblems"/>.</summary>
        public static bool Sync(string file)
        {
            var type = MapLibrary.ParseFile(file).Type;
            (object Item, MapLibraryRefs Refs) read;
            try
            {
                read = MapLibraryItemFile.Read(type, file);
            }
            catch (Exception e) when (e is InvalidOperationException or IOException)
            {
                return false;
            }

            var changed = false;
            void Visit(FieldInfo field, object value) => changed |= value is MapAssetRef link && link.Sync();
            MapLibraryFields.Walk(read.Item, Visit);
            MapLibraryFields.Walk(read.Refs, Visit);
            if (changed)
            {
                MapLibraryItemFile.Write(file, type, read.Item, read.Refs);
            }

            return changed;
        }
    }
}
