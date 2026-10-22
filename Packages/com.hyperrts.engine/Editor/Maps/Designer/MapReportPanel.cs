using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Maps.Generation;
using HyperRTS.Editor.Maps.Report;
using HyperRTS.Editor.Maps.Views;
using HyperRTS.Presentation.HUD;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// The Map Editor's report under the inspector: the newest preview's or build's issues (each linked to its
    /// feature), the preview's land figures and the last build's metrics, with Open scene and Recapture views.
    /// </summary>
    [UxmlElement]
    public sealed partial class MapReportPanel : VisualElement
    {
        private readonly VisualTreeAsset _issueRow = EditorAssets.Layout(typeof(MapReportPanel), "Issue");
        private readonly VisualTreeAsset _figureRow = EditorAssets.Layout(typeof(MapReportPanel), "Figure");
        private readonly Label _source;
        private readonly Label _empty;
        private readonly VisualElement _report;
        private readonly Label _issuesTitle;
        private readonly VisualElement _issueRows;
        private readonly Label _noIssues;
        private readonly VisualElement _landSection;
        private readonly VisualElement _landRows;
        private readonly VisualElement _metricsSection;
        private readonly VisualElement _metricRows;
        private readonly Button _openScene;
        private readonly Button _recapture;

        private MapSpecDocument _document;
        private MapReport _preview;
        private MapReport _build;
        private MapReport _issues;

        public MapReportPanel()
        {
            EditorAssets.Layout(typeof(MapReportPanel)).CloneTree(this);
            _source = this.Require<Label>("source");
            _empty = this.Require<Label>("empty");
            _report = this.Require<VisualElement>("report");
            _issuesTitle = this.Require<Label>("issues-title");
            _issueRows = this.Require<VisualElement>("issue-rows");
            _noIssues = this.Require<Label>("no-issues");
            _landSection = this.Require<VisualElement>("land");
            _landRows = this.Require<VisualElement>("land-rows");
            _metricsSection = this.Require<VisualElement>("metrics");
            _metricRows = this.Require<VisualElement>("metric-rows");
            _openScene = this.Require<Button>("open-scene");
            _openScene.clicked += OpenScene;
            _recapture = this.Require<Button>("recapture");
            _recapture.clicked += Recapture;
        }

        /// <summary>Asks to select and frame the feature an issue names.</summary>
        public event Action<MapIssueFeature> ShowFeature;

        private string ScenePath => new MapPaths(_document.Path, _document.Spec.name).Scene;

        /// <summary>Follows <paramref name="document"/> and reads the reports its last preview and build wrote.</summary>
        public void Bind(MapSpecDocument document)
        {
            _document = document;
            _preview = Current(MapReport.LoadPreview(_document.Spec.name));
            _build = Current(MapReport.Load(_document.Spec.name));
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
            var built = File.Exists(ScenePath);
            _openScene.SetEnabled(built);
            _recapture.SetEnabled(built);
            var hasReport = _issues != null;
            _empty.SetHidden(hasReport);
            _report.SetHidden(!hasReport);
            if (!hasReport)
            {
                _source.text = "Report";
                return;
            }

            var land = _preview?.land ?? _build?.land;
            var showLand = land != null && land.shares.Count > 0;
            var showMetrics = _build?.metrics != null && _build.Passed;
            _landSection.SetHidden(!showLand);
            _metricsSection.SetHidden(!showMetrics);

            var kind = _issues == _build ? "Build" : "Preview";
            _source.text = $"{kind} report, {_issues.built}";
            ShowIssues(_issues);
            if (showLand)
            {
                ShowLand(land);
            }

            if (showMetrics)
            {
                ShowMetrics(_build.metrics);
            }
        }

        /// <summary>Errors then warnings. Issues that name a feature get a Show button.</summary>
        private void ShowIssues(MapReport report)
        {
            _issuesTitle.text = $"Issues ({report.errors.Count} errors, {report.warnings.Count} warnings)";
            _issueRows.Clear();
            AddIssues(report.errors, report.errorFeatures, HelpBoxMessageType.Error);
            AddIssues(report.warnings, report.warningFeatures, HelpBoxMessageType.Warning);
            _noIssues.SetHidden(report.errors.Count + report.warnings.Count > 0);
        }

        private void AddIssues(List<string> messages, List<MapIssueFeature> features, HelpBoxMessageType kind)
        {
            for (var i = 0; i < messages.Count; i++)
            {
                var feature = features[i];
                var row = _issueRow.CloneRoot();
                var message = row.Require<HelpBox>("message");
                message.text = messages[i];
                message.messageType = kind;
                var show = row.Require<Button>("show");
                show.tooltip = $"Select and frame {feature}";
                show.clicked += () => ShowFeature?.Invoke(feature);
                show.SetHidden(feature.IsNone);
                _issueRows.Add(row);
            }
        }

        /// <summary>Land use shares and what stands on the land, from a preview or a build.</summary>
        private void ShowLand(MapLandFigures land)
        {
            _landRows.Clear();
            foreach (var share in land.shares)
            {
                AddFigure(_landRows, share.kind, Percent(share.share));
            }

            AddFigure(_landRows, "Fields", Count(land.fields));
            AddFigure(_landRows, "Trees", Count(land.trees));
            AddFigure(_landRows, "Placements", Count(land.placements));
            AddFigure(_landRows, "Decals", Count(land.decals));
            AddFigure(_landRows, "Wet decals", Count(land.wetDecals));
            AddFigure(_landRows, "Entities", Count(land.entities));
            AddFigure(_landRows, "Crop tiles", Count(land.cropTiles));
            AddFigure(_landRows, "Crop plants", land.cropPlants.ToString("N0", CultureInfo.InvariantCulture));
        }

        /// <summary>What a build measured: rush times, attack paths and each base's buildable ground and resources.</summary>
        private void ShowMetrics(MapMetrics metrics)
        {
            _metricRows.Clear();
            AddFigure(_metricRows, "Rush", $"{Seconds(metrics.minRush)} to {Seconds(metrics.maxRush)}");
            AddFigure(_metricRows, "Attack paths", Count(metrics.paths));
            AddFigure(_metricRows, "Path search", $"{metrics.maxExpansions:N0} cells");
            AddFigure(_metricRows, "Renewable fields", $"{metrics.renewableFieldsPerPlayer:0.#} per player");
            foreach (var spawn in metrics.spawns)
            {
                var text = $"team {spawn.team}, rush {Seconds(spawn.rush)}, buildable {Percent(spawn.buildable)}, "
                    + $"{spawn.finiteFields} finite fields ({spawn.finiteSupply:N0}), "
                    + $"{spawn.renewableFields} renewable ({spawn.renewableSupply:N0})";
                AddFigure(_metricRows, $"Slot {spawn.slot}", text);
            }
        }

        private void AddFigure(VisualElement rows, string name, string value)
        {
            var row = _figureRow.CloneRoot();
            row.Require<Label>("name").text = name;
            row.Require<Label>("value").text = value;
            rows.Add(row);
        }

        private static string Count(int count) => count.ToString(CultureInfo.InvariantCulture);

        private static string Percent(float share) => share.ToString("P0", CultureInfo.InvariantCulture);

        private static string Seconds(float seconds) => seconds.ToString("0' s'", CultureInfo.InvariantCulture);

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
