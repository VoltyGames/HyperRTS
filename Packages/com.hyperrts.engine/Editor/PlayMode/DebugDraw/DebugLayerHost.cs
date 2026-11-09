using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Navigation.Heights;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.DebugDraw
{
    /// <summary>Refreshes every shown <see cref="DebugLayer"/> during Play mode and drops their drawings when Play ends.</summary>
    [InitializeOnLoad]
    internal static class DebugLayerHost
    {
        private const double RefreshSeconds = 0.05;

        [NoAutoStaticsCleanup]
        private static double _nextRefresh;

        [NoAutoStaticsCleanup]
        private static readonly DebugLayer[] AllLayers =
            TypeDiscovery.Instances<DebugLayer>().OrderBy(layer => layer.Group).ThenBy(layer => layer.Label).ToArray();

        [NoAutoStaticsCleanup]
        private static readonly DebugShapes[] Shapes = AllLayers.Select(_ => new DebugShapes()).ToArray();

        static DebugLayerHost()
        {
            EditorApplication.update += Refresh;
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.ExitingPlayMode)
                {
                    DisposeAll();
                }
            };
            AssemblyReloadEvents.beforeAssemblyReload += DisposeAll;
        }

        public static IReadOnlyList<DebugLayer> Layers => AllLayers;

        /// <summary>Whether <paramref name="layer"/> drew anything on its last refresh.</summary>
        public static bool IsEmpty(DebugLayer layer) => Shapes[Array.IndexOf(AllLayers, layer)].IsEmpty;

        public static void HideAll()
        {
            for (var i = 0; i < AllLayers.Length; i++)
            {
                AllLayers[i].Shown = false;
                Shapes[i].Hide();
            }
        }

        private static void Refresh()
        {
            var due = EditorApplication.timeSinceStartup > _nextRefresh;
            if (!Application.isPlaying || !due)
            {
                return;
            }

            _nextRefresh = EditorApplication.timeSinceStartup + RefreshSeconds;
            var anyShown = false;
            for (var i = 0; i < AllLayers.Length; i++)
            {
                var layer = AllLayers[i];
                var shapes = Shapes[i];
                shapes.Clear();
                if (!layer.Shown || !PlayWorld.TryGet(layer.Source, out var entityManager))
                {
                    shapes.Hide();
                    continue;
                }

                layer.Draw(entityManager, shapes);
                PlayWorld.TryGetSingleton(entityManager, out TerrainHeight ground);
                shapes.Show(layer.Label, ground);
                anyShown = true;
            }

            // The Scene view only repaints on its own when something in the editor changes.
            if (anyShown)
            {
                SceneView.RepaintAll();
            }
        }

        private static void DisposeAll()
        {
            foreach (var shapes in Shapes)
            {
                shapes.Clear();
                shapes.Dispose();
            }
        }
    }
}
