using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Build;
using HyperRTS.Editor.Maps.Generation;
using HyperRTS.Editor.Maps.Report;
using HyperRTS.Editor.Maps.Tools;
using HyperRTS.Editor.Maps.Views;
using Unity.Pipeline.Commands;

namespace HyperRTS.Editor.Commands
{
    /// <summary>The map tools as pipeline commands: preview, build and view capture.</summary>
    public static class MapCommands
    {
        private const string Tag = "hyperrts/maps";
        private const string MapHelp = "A map name (Kalyna) or its spec path";

        [CliCommand("map_preview", "Preview a map's layout in seconds: issues, land shares, counts and the preview image path.", Tags = new[] { Tag })]
        public static object Preview([CliArg("map", MapHelp, Required = true)] string map)
        {
            var report = MapPreview.Run(MapPaths.Find(map));
            var name = report.map + MapReport.PreviewSuffix;
            return new { report.map, report.errors, report.warnings, report.land, report = MapReport.JsonPath(name), image = MapReport.ImagePath(name) };
        }

        [CliCommand("map_build", "Build a map's scene, or every map when none is named (minutes): rush times, paths, issues, the report and image paths. Fails while open scenes have unsaved changes.", Tags = new[] { Tag })]
        public static object Build([CliArg("map", MapHelp + "; empty builds every map")] string map = "")
        {
            var reports = string.IsNullOrEmpty(map)
                ? MapBuildRunner.BuildAll(ask: false)
                : new List<MapReport> { MapBuildRunner.Build(MapPaths.Find(map), ask: false) };
            return reports.Select(Summary).ToList();
        }

        [CliCommand("map_capture", "Render a built map's standard views from the match camera; returns the image paths.", Tags = new[] { Tag })]
        public static List<string> Capture([CliArg("map", MapHelp, Required = true)] string map) => MapViewCapture.Capture(MapPaths.Find(map));

        private static object Summary(MapReport report) => new
        {
            report.map,
            report.status,
            report.errors,
            report.warnings,
            report.metrics.minRush,
            report.metrics.maxRush,
            report.metrics.paths,
            report = MapReport.JsonPath(report.map),
            image = MapReport.ImagePath(report.map),
            views = MapReport.ViewsFolder(report.map),
        };
    }
}
