using HyperRTS.Editor.Maps.Build;
using HyperRTS.Editor.Maps.Tools;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// The Map Editor's previews and builds of one open document: runs the live preview onto the canvas and the
    /// report panel, builds from the toolbar, and selects and frames the feature an issue links to.
    /// </summary>
    public sealed class MapEditorPreview
    {
        private readonly MapSpecDocument _document;
        private readonly MapCanvas _canvas;
        private readonly MapFeatureSelection _selection;
        private readonly MapPreviewStrip _strip;
        private readonly MapLivePreview _live;

        public MapEditorPreview(MapSpecDocument document, MapCanvas canvas, MapFeatureSelection selection,
            MapPreviewStrip strip)
        {
            _document = document;
            _canvas = canvas;
            _selection = selection;
            _strip = strip;
            Panel = new MapReportPanel(document, Show);
            Panel.LoadFromDisk();
            _live = new MapLivePreview(document, canvas, canvas);
            _live.Finished += OnFinished;
            _live.BusyChanged += () => _strip.ShowBusy(_live.Busy);
            _strip.ShowBusy(false);
            if (MapLivePreview.Auto)
            {
                _live.Run();
            }
        }

        public MapReportPanel Panel { get; }

        public void Preview() => _live.Run();

        /// <summary>Saves the document, builds its map (asking to save open scenes first) and shows the build's report.</summary>
        public void Build()
        {
            _document.Save();
            var report = MapBuildCommands.Build(_document.Path);
            if (report != null)
            {
                Panel.ShowBuild(report);
            }
        }

        public void Dispose() => _live.Dispose();

        private void OnFinished(MapPreviewResult result)
        {
            Panel.ShowPreview(result.Report);
            if (result.Image != null)
            {
                _canvas.ShowPreview(result.Image);
            }
        }

        /// <summary>Selects and frames an issue's feature, unless edits since the report removed it.</summary>
        private void Show(MapIssueFeature issueFeature)
        {
            var kind = MapFeatureKinds.Find(issueFeature.array);
            if (issueFeature.index >= kind.Count(_document.spec))
            {
                Debug.LogWarning($"HyperRTS: {issueFeature} is no longer in the spec; preview again to refresh the report.");
                return;
            }

            var feature = new MapFeatureRef(kind, issueFeature.index);
            _selection.Set(feature);
            _canvas.Frame(feature);
        }
    }
}
