using System.IO;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Metrics;
using HyperRTS.Editor.Maps.Report;
using HyperRTS.Editor.Maps.Spec;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperRTS.Editor.Maps.Build
{
    /// <summary>
    /// Builds a map from its spec: generates the ground and everything on it, writes the scene and SubScene, measures
    /// the result and writes the report. Errors before the scene stage stop the build with a report and leave the
    /// last good scene alone.
    /// </summary>
    public static class MapBuilder
    {
        public static MapReport Build(string specPath)
        {
            var project = MapProject.Open(specPath);
            var (spec, paths) = (project.Spec, project.Paths);
            var issues = new MapIssues();
            var generation = MapGeneration.Run(spec, project, issues);
            if (generation == null)
            {
                var failed = MapReport.Create(spec, issues, new MapMetrics());
                failed.Write(null);
                return Log(failed);
            }

            var layout = generation.Layout;
            var climate = project.Climate;
            var data = MapTerrainWriter.Write(paths.Terrain, layout, generation.Heights, generation.Splat,
                generation.Details, generation.TerrainTrees, climate);
            var ground = Ground(paths, generation, climate, data);
            var (main, entities, root) = MapSceneWriter.Open(paths, layout, project);
            MapEnvironmentWriter.Write(paths, layout, generation.Shapes, data, ground, climate, entities, root);
            MapRoadMeshWriter.Write(paths, layout, generation.Shapes, generation.Heights, root);
            MapPlacementWriter.Write(generation.Placer.Placements, generation.Heights, layout.Datum, entities, root);
            EditorSceneManager.SaveScene(main);
            MapLightingBaker.Bake(paths);
            var minimap = MapMinimapCapture.Capture(layout, paths.Minimap);
            MapEnvironmentWriter.SetMinimap(root, minimap);
            var report = Measure(spec, generation, project, data, entities, issues);
            EditorSceneManager.SaveScene(entities);
            EditorSceneManager.SaveScene(main);
            EditorSceneManager.CloseScene(entities, true);
            AssetDatabase.SaveAssets();
            var overview = CaptureViews(spec, paths, generation, project);
            Log(report);
            var result = new MapBuildResult(spec, paths, report, layout.Spawns.Count, minimap, overview);
            foreach (var hook in TypeDiscovery.Instances<IMapBuildHook>())
            {
                hook.OnBuilt(result);
            }

            return report;
        }

        /// <summary>The terrain material with its macro map, and the ground colour picture foliage blends into.</summary>
        private static MapGroundLook Ground(MapPaths paths, MapGeneration generation, MapClimate climate, TerrainData data)
        {
            var macro = MapMacroPainter.Write(paths.Macro, generation.Layout, generation.Land, generation.Shapes);
            var material = MapTerrainMaterial.Write(paths.TerrainMaterial, climate, data, macro, generation.RowAngle);
            var picture = MapGroundColor.Write(paths.Ground, generation.Splat, climate, macro);
            return new MapGroundLook(material, picture);
        }

        private static MapReport Measure(MapSpec spec, MapGeneration generation, MapProject project, TerrainData data,
            Scene entities, MapIssues issues)
        {
            var layout = generation.Layout;
            var origin = new Vector3(layout.TerrainBounds.xMin, -layout.Datum, layout.TerrainBounds.yMin);
            var water = layout.Terrain.waterLevel - layout.Datum;
            using var nav = MapNavGrid.Build(entities, data, origin, layout.Bounds, water, project.Rules);
            var measurer = MapMeasurer.Measure(layout, nav, generation.Placer, generation.Trees.Count, project.Palette,
                project.Rules, issues);
            MapHintWriter.Write(entities, layout, generation.Shapes, generation.Placer.Placements, nav, project.Palette,
                project.Rules, issues);
            var report = MapReport.Create(spec, issues, measurer.Metrics);
            report.Write(MapOverviewImage.Draw(layout, generation.Heights, generation.Splat, project.Climate, nav,
                generation.Shapes, generation.Placer.Placements, measurer.Paths));
            return report;
        }

        /// <summary>
        /// Fresh screenshots with every build, the overview kept next to the map as its picture; none (null) when
        /// there's no graphics device (batch mode with -nographics).
        /// </summary>
        private static Texture2D CaptureViews(MapSpec spec, MapPaths paths, MapGeneration generation, MapProject project)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                return null;
            }

            var views = MapViews.For(generation.Layout, generation.Shapes, project.Rules);
            var files = MapViews.Capture(spec.name, views, project.Rules);
            return MapOverviewPicture.Keep(files.Find(f => Path.GetFileNameWithoutExtension(f) == MapViews.OverviewName), paths.Overview);
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
