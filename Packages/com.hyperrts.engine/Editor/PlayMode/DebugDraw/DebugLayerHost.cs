using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.DebugDraw
{
    /// <summary>Hosts every <see cref="DebugLayer"/> in the Scene view during Play mode.</summary>
    [InitializeOnLoad]
    internal static class DebugLayerHost
    {
        private const double RepaintSeconds = 0.1;

        private static double _nextRepaint;

        static DebugLayerHost()
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
            var due = EditorApplication.timeSinceStartup > _nextRepaint;
            if (!Application.isPlaying || !due || !Active)
            {
                return;
            }

            _nextRepaint = EditorApplication.timeSinceStartup + RepaintSeconds;
            SceneView.RepaintAll();
        }

        private static void OnSceneGUI(SceneView view)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            foreach (var layer in AllLayers)
            {
                if (layer.Enabled && PlayWorld.TryGet(layer.Source, out var entityManager))
                {
                    layer.Draw(entityManager);
                }
            }
        }
    }
}
