using HyperRTS.Editor.Maps.Generation;
using HyperRTS.Editor.Maps.Report;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Build
{
    /// <summary>
    /// A map's layout in seconds without touching a scene: the refined features, ground shape, land use, placements
    /// and trees, drawn to <c>Logs/MapReports/&lt;Map&gt;.preview.png</c> with the issues and land figures in
    /// <c>.preview.json</c>. Rush times and paths need the full build.
    /// </summary>
    public static class MapPreview
    {
        public static MapReport Run(string specPath)
        {
            var project = MapProject.Open(specPath);
            var spec = project.Spec;
            var issues = new MapIssues();
            var generation = MapGeneration.Plan(project, issues);
            var report = MapReport.Create(spec, issues, new MapMetrics());
            Texture2D image = null;
            if (generation != null)
            {
                report.land = MapLandFigures.Measure(generation);
                image = MapOverviewImage.DrawLand(generation);
            }

            report.WritePreview(image);
            Debug.Log($"HyperRTS: map '{spec.name}' previewed with {report.errors.Count} errors and {report.warnings.Count} warnings. Report: {MapReport.JsonPath(spec.name + MapReport.PreviewSuffix)}");
            return report;
        }
    }
}
