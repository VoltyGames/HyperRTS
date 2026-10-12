using HyperRTS.Editor.Maps.Library;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Factories
{
    /// <summary>
    /// Regenerates a library item's assets when its JSON file imports (saved in the Library page or edited by hand).
    /// The files of one import batch run together in a single delayed call, after the import has finished.
    /// </summary>
    public sealed class MapAssetPostprocessor : AssetPostprocessor
    {
        private static readonly MapAssetQueue Queue = new();

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var file in imported)
            {
                if (MapLibrary.ParseFile(file).Type != null && Queue.Add(file))
                {
                    EditorApplication.delayCall += Flush;
                }
            }
        }

        private static void Flush()
        {
            var files = Queue.Take();
            var written = MapLibraryAssets.GenerateFiles(MapLibrary.Load(), files);
            if (written.Count > 0)
            {
                Debug.Log($"[HyperRTS] Regenerated {written.Count} map library assets from {files.Count} changed item files.");
            }
        }
    }
}
