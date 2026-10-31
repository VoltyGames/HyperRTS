using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Generation;
using HyperRTS.Editor.Maps.Report;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Views
{
    /// <summary>
    /// Opens a built map's scene if it isn't open, renders its standard views and updates the last build's view
    /// contrast.
    /// </summary>
    public static class MapViewCapture
    {
        public static List<string> Capture(string specPath)
        {
            using var project = MapProject.Open(specPath);
            if (EditorSceneManager.GetActiveScene().path != project.Paths.Scene)
            {
                EditorSceneManager.OpenScene(project.Paths.Scene, OpenSceneMode.Single);
            }

            var issues = new MapIssues();
            var generation = MapGeneration.Lay(project, issues)
                ?? throw new InvalidOperationException($"HyperRTS: map '{project.Spec.name}' has errors:\n{issues}");

            var name = project.Spec.name;
            var views = MapViews.For(generation.Layout, generation.Shapes, project.Rules);
            var files = MapViews.Capture(name, views);
            var report = MapReport.Load(name);
            if (report != null)
            {
                report.metrics.MeasureViews(files, MapViews.OverviewName);
                report.Write(null);
            }

            Debug.Log($"HyperRTS: captured {files.Count} views of '{name}' in {MapReport.ViewsFolder(name)}");
            return files;
        }
    }
}
