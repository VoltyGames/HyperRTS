using UnityEditor;

namespace HyperRTS.Editor.Validation
{
    /// <summary>Drops the kept prefab and asset sets after any import, delete or move, so the next pass loads them afresh.</summary>
    public sealed class ValidationImport : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            PrefabSet.Forget();
            AssetSet.Forget();
        }
    }
}
