using System.IO;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Maps.Generation;
using HyperRTS.Editor.Maps.Hints;
using HyperRTS.Editor.Maps.Meshes;
using HyperRTS.Editor.Maps.Nav;
using HyperRTS.Editor.Maps.Report;
using HyperRTS.Editor.Maps.Scenes;
using HyperRTS.Editor.Maps.Views;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Editor.Maps.Build
{
    /// <summary>Builds a map's scene, SubScene and report from its spec. Errors before the scene stage keep the last good scene.</summary>
    public static class MapBuilder
    {
        public static MapReport Build(string specPath)
        {
            var project = MapProject.Open(specPath);
            var issues = new MapIssues();
            var generation = MapGeneration.Run(project, issues);
            if (generation == null)
            {
                var failed = MapReport.Create(project.Spec, issues, new MapMetrics());
                failed.Write(null);
                return Log(failed);
            }

            project.Paths.EnsureGenerated();
            var data = MapTerrainWriter.Write(generation);
            var ground = MapEnvironmentWriter.WriteGround(generation, data);
            var scene = MapScene.Open(generation);
            MapEnvironmentWriter.Write(scene, generation, data, ground);
            MapRoadMeshWriter.Write(scene, generation);
            MapPlacementWriter.Write(scene, generation);
            MapDecalWriter.Write(scene, generation);
            scene.SaveMain();
            var minimap = Render(generation);
            MapEnvironmentWriter.SetMinimap(scene, minimap);
            var report = Measure(generation, scene, data, issues);
            scene.SaveAndCloseEntities();
            AssetDatabase.SaveAssets();
            var overview = CaptureViews(generation);
            Log(report);
            var result = new MapBuildResult(project.Spec, project.Paths, report, generation.Layout.Spawns.Count, minimap,
                overview);
            foreach (var hook in TypeDiscovery.Instances<IMapBuildHook>())
            {
                hook.OnBuilt(result);
            }

            return report;
        }

        /// <summary>Measures the map on the simulation's nav grid and writes its bot hints and report.</summary>
        private static MapReport Measure(MapGeneration generation, MapScene scene, TerrainData data, MapIssues issues)
        {
            var layout = generation.Layout;
            using var nav = MapNavGrid.Build(scene.Entities, data, layout.TerrainOrigin, layout.Bounds, layout.WaterHeight,
                generation.Project.Rules);
            var measurer = MapMeasurer.Measure(generation, nav, issues);
            MapHintWriter.Write(scene, generation, nav, issues);
            var report = MapReport.Create(generation.Project.Spec, issues, measurer.Metrics);
            report.land = MapLandFigures.Measure(generation);
            report.Write(MapOverviewImage.Draw(generation, nav, measurer.Paths));
            return report;
        }

        // Without a graphics device (batch mode with -nographics) nothing renders, so the last lighting and pictures stay.
        private static bool HasGraphics => SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;

        /// <summary>Bakes the lighting and captures the minimap, or keeps the last minimap without graphics.</summary>
        private static Texture2D Render(MapGeneration generation)
        {
            var paths = generation.Project.Paths;
            if (HasGraphics)
            {
                MapLightingBaker.Bake(paths);
                return MapMinimapCapture.Capture(generation.Layout, paths.Minimap);
            }

            var kept = AssetDatabase.LoadAssetAtPath<Texture2D>(paths.Minimap);
            if (kept == null)
            {
                throw new System.InvalidOperationException(
                    $"HyperRTS: the first build of '{paths.Name}' needs graphics for its minimap ({paths.Minimap}): build it in the editor.");
            }

            return kept;
        }

        /// <summary>Screenshots of the map, or null without a graphics device.</summary>
        private static Texture2D CaptureViews(MapGeneration generation)
        {
            if (!HasGraphics)
            {
                return null;
            }

            var project = generation.Project;
            var views = MapViews.For(generation.Layout, generation.Shapes, project.Rules);
            var files = MapViews.Capture(project.Spec.name, views, project.Rules);
            var overview = files.Find(f => Path.GetFileNameWithoutExtension(f) == MapViews.OverviewName);
            return MapOverviewPicture.Save(overview, project.Paths.Overview);
        }

        private static MapReport Log(MapReport report)
        {
            var summary = $"HyperRTS: map '{report.map}' built with {report.errors.Count} errors and {report.warnings.Count} warnings. Report: {MapReport.JsonPath(report.map)}";
            if (report.Passed)
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogError(summary + "\n" + string.Join("\n", report.errors));
            }

            return report;
        }
    }
}
