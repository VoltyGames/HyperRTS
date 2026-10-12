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
            var generation = MapGeneration.Plan(spec, project, issues);
            var report = MapReport.Create(spec, issues, new MapMetrics());
            Texture2D image = null;
            if (generation != null)
            {
                var layout = generation.Layout;
                report.land = MapLandFigures.Measure(generation.Land, layout.Bounds, generation.Trees.Count,
                    generation.Placements.All);
                image = MapOverviewImage.DrawLand(layout, generation.Heights, generation.Land, project.Climate,
                    generation.Shapes, generation.Placements.All, generation.Trees);
            }

            report.WritePreview(image);
            Debug.Log($"HyperRTS: map '{spec.name}' previewed with {report.errors.Count} errors and {report.warnings.Count} warnings. Report: {MapReport.JsonPath(spec.name + MapReport.PreviewSuffix)}");
            return report;
        }
    }
}
