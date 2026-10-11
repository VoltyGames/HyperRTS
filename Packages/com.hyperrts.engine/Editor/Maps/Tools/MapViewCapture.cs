using System.Collections.Generic;
using System.IO;
using HyperRTS.Editor.Maps.Build;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Report;
using HyperRTS.Editor.Maps.Spec;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Tools
{
    /// <summary>Opens a built map's scene if it isn't open and renders its standard views.</summary>
    public static class MapViewCapture
    {
        public static List<string> Capture(string specPath)
        {
            var spec = JsonUtility.FromJson<MapSpec>(File.ReadAllText(specPath));
            var paths = new MapPaths(specPath, spec.name);
            if (EditorSceneManager.GetActiveScene().path != paths.Scene)
            {
                EditorSceneManager.OpenScene(paths.Scene, OpenSceneMode.Single);
            }

            var project = MapProject.Load(spec.climate);
            var issues = new MapIssues();
            MapRefiner.Refine(spec, project.Climate, issues);
            var layout = MapExpander.Expand(spec, issues);
            var shapes = new MapShapes(layout, project.Palette, project.Climate, issues);
            var views = MapViews.For(layout, shapes, project.Rules);
            var files = MapViews.Capture(spec.name, views, project.Rules);
            Debug.Log($"HyperRTS: captured {files.Count} views of '{spec.name}' in {MapReport.ViewsFolder(spec.name)}");
            return files;
        }
    }
}
