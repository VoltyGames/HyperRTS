using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Effects.Preview
{
    /// <summary>Hands every import to the open Effect Previews, so a saved recipe rebuilds its graph and replays.</summary>
    public sealed class EffectPreviewImport : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<EffectPreviewWindow>())
            {
                window.OnImported(imported);
            }
        }
    }
}
