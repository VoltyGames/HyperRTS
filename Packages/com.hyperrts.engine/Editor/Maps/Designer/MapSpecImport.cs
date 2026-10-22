using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>Hands every import to the open Map Editors, so a saved spec redraws without polling the disk.</summary>
    public sealed class MapSpecImport : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<MapEditorWindow>())
            {
                window.OnImported(imported);
            }
        }
    }
}
