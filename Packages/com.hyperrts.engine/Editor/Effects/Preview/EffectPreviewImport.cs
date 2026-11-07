using System;
using UnityEditor;

namespace HyperRTS.Editor.Effects.Preview
{
    /// <summary>Hands every import to the shown Effects page, so a saved recipe rebuilds its graph and replays.</summary>
    public sealed class EffectPreviewImport : AssetPostprocessor
    {
        public static event Action<string[]> Imported;

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom) =>
            Imported?.Invoke(imported);
    }
}
