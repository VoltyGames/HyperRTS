using System;
using System.IO;
using HyperRTS.Editor.Maps.Generation;
using HyperRTS.Editor.Maps.Report;
using HyperRTS.Editor.Maps.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// The Map Editor's report under the inspector: the newest preview's or build's issues (each linked to its
    /// feature), the preview's land figures and the last build's metrics, with Open scene and Recapture views.
    /// </summary>
    public sealed class MapReportPanel : VisualElement
    {
        private readonly MapSpecDocument _document;
        private readonly Action<MapIssueFeature> _show;
        private readonly Label _source;
        private readonly ScrollView _content;
        private readonly Button _openScene;
        private readonly Button _recapture;

        private MapReport _preview;
        private MapReport _build;
        private MapReport _issues;

        public MapReportPanel(MapSpecDocument document, Action<MapIssueFeature> show)
        {
            _document = document;
            _show = show;
            AddToClassList("hrts-map-report");
            _source = new Label();
            _source.AddToClassList("hrts-section__title");
            Add(_source);
            _content = new ScrollView();
            _content.AddToClassList("hrts-map-report__content");
            Add(_content);

            var buttons = new VisualElement();
            buttons.AddToClassList("hrts-buttons");
            _openScene = new Button(OpenScene) { text = "Open scene" };
            _recapture = new Button(Recapture) { text = "Recapture views" };
            buttons.Add(_openScene);
            buttons.Add(_recapture);
            Add(buttons);
        }

        private string ScenePath => new MapPaths(_document.Path, _document.spec.name).Scene;

        /// <summary>Reads the reports the last preview and build wrote for the open spec.</summary>
        public void LoadFromDisk()
        {
            _preview = Current(MapReport.LoadPreview(_document.spec.name));
            _build = Current(MapReport.Load(_document.spec.name));
            _issues = Newest(_preview, _build);
            Refresh();
        }

        public void ShowPreview(MapReport report)
        {
            _preview = report;
            _issues = report;
            Refresh();
        }

        public void ShowBuild(MapReport report)
        {
            _build = report;
            _issues = report;
            Refresh();
        }

        /// <summary>A report written before issues named their features is stale: it is left out until rebuilt.</summary>
        private static MapReport Current(MapReport report)
        {
            if (report == null)
            {
                return null;
            }

            var errorsLinked = report.errorFeatures.Count == report.errors.Count;
            var warningsLinked = report.warningFeatures.Count == report.warnings.Count;
            return errorsLinked && warningsLinked ? report : null;
        }

        /// <summary>The later of two reports; their <c>built</c> stamps sort as text.</summary>
        private static MapReport Newest(MapReport a, MapReport b)
        {
            if (a == null || b == null)
            {
                return a ?? b;
            }

            return string.CompareOrdinal(a.built, b.built) >= 0 ? a : b;
        }

        private void Refresh()
        {
            _content.Clear();
            var built = File.Exists(ScenePath);
            _openScene.SetEnabled(built);
            _recapture.SetEnabled(built);
            if (_issues == null)
            {
                _source.text = "Report";
                _content.Add(MapReportSections.Note("No preview or build yet."));
                return;
            }

            var kind = _issues == _build ? "Build" : "Preview";
            _source.text = $"{kind} report, {_issues.built}";
            _content.Add(MapReportSections.Issues(_issues, _show));
            var land = _preview?.land ?? _build?.land;
            if (land != null && land.shares.Count > 0)
            {
                _content.Add(MapReportSections.Land(land));
            }

            if (_build?.metrics != null && _build.Passed)
            {
                _content.Add(MapReportSections.Metrics(_build.metrics));
            }
        }

        private void OpenScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }

        private void Recapture()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                MapViewCapture.Capture(_document.Path);
            }
        }
    }
}
