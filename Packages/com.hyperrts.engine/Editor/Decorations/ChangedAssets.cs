using System;
using System.Linq;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;

namespace HyperRTS.Editor.Decorations;

/// <summary>Tells the Project window decorations which asset paths an import added, changed, moved or deleted.</summary>
internal sealed class ChangedAssets : AssetPostprocessor
{
    [NoAutoStaticsCleanup]
    private static Action<string[]> _changed;

    public static void Subscribe(Action<string[]> handler) => _changed += handler;

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom, bool didDomainReload)
    {
        if (_changed == null)
        {
            return;
        }

        var paths = imported.Concat(deleted).Concat(moved).Concat(movedFrom).ToArray();
        if (paths.Length > 0)
        {
            _changed(paths);
        }
    }
}
