using System.Threading.Tasks;
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
        /// <summary>Previews the spec on disk and writes the preview report and image.</summary>
        public static MapReport Run(string specPath)
        {
            var result = Run(MapProject.Open(specPath));
            var report = result.Report;
            report.WritePreview(result.Image);
            Debug.Log($"HyperRTS: map '{report.map}' previewed with {report.errors.Count} errors and {report.warnings.Count} warnings. Report: {MapReport.JsonPath(report.map + MapReport.PreviewSuffix)}");
            return report;
        }

        /// <summary>Previews a project (an editor's in-memory spec) without writing anything.</summary>
        public static MapPreviewResult Run(MapProject project)
        {
            var issues = new MapIssues();
            return Finish(project, issues, MapGeneration.Plan(project, issues));
        }

        /// <summary>
        /// <see cref="Run(MapProject)"/> with the planning on a worker thread. Call it on the main thread: the checks
        /// before and the measuring and drawing after need Unity objects and run there.
        /// </summary>
        public static async Task<MapPreviewResult> RunAsync(MapProject project)
        {
            var issues = new MapIssues();
            MapGeneration generation = null;
            if (MapGeneration.Check(project, issues))
            {
                generation = await Task.Run(() => MapGeneration.PlanChecked(project, issues));
            }

            return Finish(project, issues, generation);
        }

        private static MapPreviewResult Finish(MapProject project, MapIssues issues, MapGeneration generation)
        {
            var report = MapReport.Create(project.Spec, issues, new MapMetrics());
            Texture2D image = null;
            if (generation != null)
            {
                report.land = MapLandFigures.Measure(generation);
                image = MapOverviewImage.DrawLand(generation);
            }

            return new MapPreviewResult(report, image);
        }
    }
}
