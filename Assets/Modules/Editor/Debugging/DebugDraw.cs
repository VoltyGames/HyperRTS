using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Debugging
{
    /// <summary>Hosts every <see cref="DebugLayer"/> in the Scene view during Play mode.</summary>
    [InitializeOnLoad]
    internal static class DebugDraw
    {
        private const double RepaintSeconds = 0.1;

        private static double _nextRepaint;

        static DebugDraw()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += RepaintWhileActive;
        }

        public static IReadOnlyList<DebugLayer> Layers { get; } =
            TypeDiscovery.Instances<DebugLayer>().OrderBy(layer => layer.Label).ToArray();

        private static bool Active => Layers.Any(layer => layer.Enabled);

        // Entity changes don't repaint the Scene view on their own.
        private static void RepaintWhileActive()
        {
            if (Active && Application.isPlaying && EditorApplication.timeSinceStartup > _nextRepaint)
            {
                _nextRepaint = EditorApplication.timeSinceStartup + RepaintSeconds;
                SceneView.RepaintAll();
            }
        }

        private static void OnSceneGUI(SceneView view)
        {
            if (!Active || Event.current.type != EventType.Repaint || !PlayWorld.TryGet(out var entityManager))
            {
                return;
            }

            foreach (var layer in Layers)
            {
                if (layer.Enabled)
                {
                    layer.Draw(entityManager);
                }
            }
        }
    }
}
