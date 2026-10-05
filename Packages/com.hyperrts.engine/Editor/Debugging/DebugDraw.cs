using System;
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

        private static readonly DebugLayer[] AllLayers =
            TypeDiscovery.Instances<DebugLayer>().OrderBy(layer => layer.Label).ToArray();

        public static IReadOnlyList<DebugLayer> Layers => AllLayers;

        private static bool Active => Array.Exists(AllLayers, layer => layer.Enabled);

        // Entity changes don't repaint the Scene view on their own.
        private static void RepaintWhileActive()
        {
            if (Application.isPlaying && EditorApplication.timeSinceStartup > _nextRepaint && Active)
            {
                _nextRepaint = EditorApplication.timeSinceStartup + RepaintSeconds;
                SceneView.RepaintAll();
            }
        }

        private static void OnSceneGUI(SceneView view)
        {
            if (Event.current.type != EventType.Repaint || !Active || !PlayWorld.TryGet(out var entityManager))
            {
                return;
            }

            foreach (var layer in AllLayers)
            {
                if (layer.Enabled)
                {
                    layer.Draw(entityManager);
                }
            }
        }
    }
}
