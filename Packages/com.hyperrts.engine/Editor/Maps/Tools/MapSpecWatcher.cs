using System.Linq;
using HyperRTS.Editor.Maps.Generation;
using UnityEditor;

namespace HyperRTS.Editor.Maps.Tools
{
    /// <summary>Rebuilds a map when its spec changes on disk, while HyperRTS ▸ Maps ▸ Auto-rebuild is on.</summary>
    public sealed class MapSpecWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved,
            string[] movedFrom)
        {
            if (!MapMenu.AutoRebuildOn || UnityEngine.Application.isBatchMode)
            {
                return;
            }

            foreach (var spec in imported.Where(MapPaths.IsSpec))
            {
                // Scenes can't be opened during an import; build once it has finished.
                EditorApplication.delayCall += () => MapMenu.BuildSpec(spec);
            }
        }
    }
}
