using System.Collections.Generic;
using UnityEditor;

namespace HyperRTS.Editor.Scenes
{
    /// <summary>
    /// Gives each Scene view fixed clip planes at map scale. Unity's dynamic clipping sets the near plane from the
    /// distance to the view's pivot, which drifts as you zoom and fly, so it cut away the ground in front of the camera.
    /// </summary>
    [InitializeOnLoad]
    internal static class SceneViewClipping
    {
        private const float Near = 0.1f;

        // Room for a whole map and its horizon seen from high above; the match camera reaches 3 km at most.
        private const float Far = 5000f;

        private static readonly HashSet<SceneView> Configured = new();

        static SceneViewClipping() => SceneView.duringSceneGui += Apply;

        // Once per view, so a view's own settings can still be changed afterwards.
        private static void Apply(SceneView view)
        {
            if (!Configured.Add(view))
            {
                return;
            }

            var settings = view.cameraSettings;
            settings.dynamicClip = false;
            settings.nearClip = Near;
            settings.farClip = Far;
            view.cameraSettings = settings;
        }
    }
}
