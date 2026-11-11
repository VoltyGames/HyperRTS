using HyperRTS.Editor.Maps.Common;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Maps.Hints;
using HyperRTS.Editor.Maps.Meshes;
using HyperRTS.Editor.Maps.Nav;
using HyperRTS.Editor.Maps.Report;
using HyperRTS.Editor.Maps.Scenes;
using HyperRTS.Editor.Maps.Views;
using HyperRTS.Presentation.Common;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Build
{
    /// <summary>Builds a map's scene, SubScene and report from its spec. Errors before the scene stage keep the last good scene.</summary>
    public static class MapBuilder
    {
        public static MapReport Build(string specPath)
        {
            using var project = MapProject.Open(specPath);
            var issues = new MapIssues();
            var generation = MapGeneration.Run(project, issues);
            if (generation == null)
            {
                var failed = MapReport.Create(project.Spec, issues, new MapMetrics());
                failed.Write(null);
                return Log(failed);
            }

            project.Paths.EnsureGenerated();
            var timer = generation.Timer;
            var data = timer.Time("terrain", () => MapTerrainWriter.Write(generation));
            var ground = timer.Time("ground", () => MapEnvironmentWriter.WriteGround(generation, data));
            var scene = MapScene.Open(generation);
            MapEnvironmentWriter.Write(scene, generation, data, ground);
            MapRoadMeshWriter.Write(scene, generation);
            MapPlacementWriter.Write(scene, generation);
            MapDecalWriter.Write(scene, generation);
            scene.SaveMain();
            var minimap = timer.Time("lighting", () => Render(generation));
            MapEnvironmentWriter.SetMinimap(scene, minimap);
            var report = Measure(generation, scene, data, issues);
            scene.SaveAndCloseEntities();
            AssetDatabase.SaveAssets();
            var overview = CaptureViews(generation, report);
            Log(report);
            NotifyHooks(project, generation, report, minimap, overview);

            return report;
        }

        private static void NotifyHooks(MapProject project, MapGeneration generation, MapReport report, Texture2D minimap, Texture2D overview)
        {
            var result = new MapBuildResult
            {
                Spec = project.Spec,
                Paths = project.Paths,
                Climate = project.Climate,
                Report = report,
                Players = generation.Layout.Spawns.Count,
                Minimap = minimap,
                Overview = overview,
            };
            foreach (var hook in TypeDiscovery.Instances<IMapBuildHook>())
            {
                hook.OnBuilt(result);
            }
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
            report.land = MapLandShares.Measure(generation);
            report.stages.AddRange(generation.Timer.Stages);
            report.Write(MapReportImage.Draw(generation, nav, measurer.Paths));
            return report;
        }

        /// <summary>Bakes the lighting and captures the minimap, or keeps the last minimap without graphics.</summary>
        private static Texture2D Render(MapGeneration generation)
        {
            var paths = generation.Project.Paths;
            if (GraphicsSupport.Available)
            {
                MapLightingBaker.Bake(paths);
                return MapMinimapCapture.Capture(generation.Layout, paths.Minimap);
            }

            return Kept(paths.Name, paths.Minimap);
        }

        /// <summary>
        /// Screenshots the map, adds the views' contrast to the report and saves the overview, or keeps the last
        /// overview without graphics.
        /// </summary>
        private static Texture2D CaptureViews(MapGeneration generation, MapReport report)
        {
            var project = generation.Project;
            if (!GraphicsSupport.Available)
            {
                return Kept(project.Paths.Name, project.Paths.Overview);
            }

            var views = MapViews.For(generation.Layout, generation.Shapes, project.Rules);
            var files = MapViews.Capture(project.Spec.name, views);
            report.metrics.MeasureViews(files, MapViews.OverviewName);
            report.Write(null);
            var overview = files.Find(f => Path.GetFileNameWithoutExtension(f) == MapViews.OverviewName);
            return MapOverviewPicture.Save(overview, project.Paths.Overview);
        }

        private static Texture2D Kept(string map, string path)
        {
            var kept = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (kept == null)
            {
                throw new System.InvalidOperationException(
                    $"HyperRTS: the first build of '{map}' needs graphics for {path}: build it in the editor.");
            }

            return kept;
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
