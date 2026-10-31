using System;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Common
{
    /// <summary>Reloads every open <see cref="JsonDocument"/> whose file was imported, so an outside write shows without polling.</summary>
    public sealed class JsonDocumentImport : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var document in Resources.FindObjectsOfTypeAll<JsonDocument>())
            {
                if (Array.IndexOf(imported, document.Path) < 0)
                {
                    continue;
                }

                // After the import, since a reload can start work that loads other assets (a map preview).
                EditorApplication.delayCall += () =>
                {
                    if (document != null)
                    {
                        document.Reload();
                    }
                };
            }
        }
    }
}
