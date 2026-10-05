using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Validation
{
    /// <summary>Logs open-scene problems when entering Play mode, each linked to its object.</summary>
    [InitializeOnLoad]
    internal static class PlayModeValidation
    {
        static PlayModeValidation() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode)
            {
                return;
            }

            foreach (var issue in ProjectValidator.OpenScenes())
            {
                if (issue.Severity == MessageType.Error)
                {
                    Debug.LogError(issue, issue.Context);
                }
                else if (issue.Severity == MessageType.Warning)
                {
                    Debug.LogWarning(issue, issue.Context);
                }
            }
        }
    }
}
