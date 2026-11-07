using System;
using System.Linq;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;

namespace HyperRTS.Editor.Effects.Preview
{
    /// <summary>
    /// Hands imports to the shown Effects page, so a saved recipe rebuilds its graph and replays, and a changed rig,
    /// graph or recipe lists the rigs again.
    /// </summary>
    public sealed class EffectPreviewImport : AssetPostprocessor
    {
        // The page that listens unsubscribes when it closes.
        [NoAutoStaticsCleanup]
        public static event Action<string[]> Imported;

        /// <summary>A prefab, VFX graph or recipe was imported, deleted or moved.</summary>
        [NoAutoStaticsCleanup]
        public static event Action EffectsChanged;

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            Imported?.Invoke(imported);
            if (imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(IsEffectAsset))
            {
                EffectsChanged?.Invoke();
            }
        }

        private static bool IsEffectAsset(string path) =>
            path.EndsWith(".prefab", StringComparison.Ordinal) || path.EndsWith(".vfx", StringComparison.Ordinal) ||
            EffectRecipe.IsRecipe(path);
    }
}
