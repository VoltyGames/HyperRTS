using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Build;
using HyperRTS.Editor.Maps.Report;
using HyperRTS.Editor.Maps.Tools;
using HyperRTS.Editor.Maps.Views;
using Unity.Pipeline.Commands;

namespace HyperRTS.Editor.Maps.Tools
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

        [CliCommand("map_look", "Start Play-mode captures of a built map's spec looks at three zooms per weather, with GI settled (minutes). Returns at once with the folder; poll map_look_status until it says Done or Failed. Weathers is a comma list, empty for all; quality a level name, empty for the current one.", Tags = new[] { Tag })]
        public static object Look(
            [CliArg("map", MapHelp, Required = true)] string map,
            [CliArg("weathers", "Comma list of weather keys; empty takes every weather")] string weathers = "",
            [CliArg("quality", "Quality level name; empty keeps the current level")] string quality = "",
            [CliArg("graded", "False turns the weathers' Color Lookup off, for captures to fit lookups from")] bool graded = true,
            [CliArg("cost", "True measures the median GPU and CPU frame time per look view with each feature off in turn, in the first weather, into cost.json, and takes no images")] bool cost = false) =>
            new { folder = MapLookCapture.Start(MapPaths.Find(map), weathers, graded, quality, cost) };

        [CliCommand("map_look_status", "Progress of the running look capture, or how the last one ended.", Tags = new[] { Tag })]
        public static object LookStatus() => new { running = MapLookCapture.IsRunning, status = MapLookCapture.Status };

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
