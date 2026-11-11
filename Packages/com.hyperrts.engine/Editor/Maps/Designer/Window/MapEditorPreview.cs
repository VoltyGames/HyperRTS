using System;
using HyperRTS.Editor.Maps.Build;
using HyperRTS.Editor.Maps.Common;
using HyperRTS.Editor.Maps.Designer.Canvas;
using HyperRTS.Editor.Maps.Designer.Features;
using HyperRTS.Editor.Maps.Tools;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Designer.Window
{
    /// <summary>
    /// The Map Editor's previews and builds of one open document: runs the live preview onto the canvas and the
    /// issue and report tabs, builds from the toolbar, and selects, frames and marks the feature an issue links to.
    /// </summary>
    public sealed class MapEditorPreview
    {
        private static readonly Color ErrorColor = new(0.87f, 0.25f, 0.2f);
        private static readonly Color WarningColor = new(0.95f, 0.7f, 0.1f);

        private readonly MapSpecDocument _document;
        private readonly MapCanvas _canvas;
        private readonly MapFeatureSelection _selection;
        private readonly MapPreviewStrip _strip;
        private readonly MapReportPanel _panel;
        private readonly MapIssueList _issues;
        private readonly MapLivePreview _live;

        public MapEditorPreview(MapSpecDocument document, MapCanvas canvas, MapFeatureSelection selection,
            MapPreviewStrip strip, MapReportPanel panel, MapIssueList issues)
        {
            _document = document;
            _canvas = canvas;
            _selection = selection;
            _strip = strip;
            _panel = panel;
            _issues = issues;
            _panel.Bind(document);
            _issues.Show(_panel.Issues);
            _live = new MapLivePreview(document);
            _live.Finished += OnFinished;
            _live.BusyChanged += () => _strip.ShowBusy(_live.Busy);
            _strip.ShowBusy(false);
            if (MapLivePreview.Auto)
            {
                _live.Run();
            }
        }

        /// <summary>A preview finished.</summary>
        public event Action Previewed;

        public void Preview() => _live.Run();

        /// <summary>Builds the spec's map from its file (asking to save open scenes first) and shows the build's report.</summary>
        public void Build()
        {
            var report = MapBuildRunner.Build(_document.Path);
            if (report != null)
            {
                _panel.ShowBuild(report);
                _issues.Show(report);
            }
        }

        public void Dispose() => _live.Dispose();

        private void OnFinished(MapPreviewResult result)
        {
            _panel.ShowPreview(result.Report);
            _issues.Show(result.Report);
            if (result.Image != null)
            {
                _canvas.ShowPreview(result.Image);
            }

            Previewed?.Invoke();
        }

        /// <summary>Selects, frames and marks an issue's feature, unless the spec changed since the report and dropped it.</summary>
        public void Show(MapIssueFeature issueFeature, bool error)
        {
            var kind = MapFeatureKinds.Find(issueFeature.array);
            if (issueFeature.index >= kind.Count(_document.Spec))
            {
                Debug.LogWarning($"HyperRTS: {issueFeature} is no longer in the spec; preview again to refresh the report.");
                return;
            }

            var feature = new MapFeatureRef(kind, issueFeature.index);
            _selection.Set(feature);
            _canvas.Frame(feature);
            _canvas.Mark(feature, error ? ErrorColor : WarningColor);
        }
    }
}
