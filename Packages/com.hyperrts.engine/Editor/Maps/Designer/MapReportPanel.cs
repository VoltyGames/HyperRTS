using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Maps.Build;
using HyperRTS.Editor.Maps.Common;
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
        private readonly Label _source;
        private readonly Label _empty;
        private readonly VisualElement _report;
        private readonly Section _issuesSection;
        private readonly VisualElement _issueRows;
        private readonly Label _noIssues;
        private readonly VisualElement _landSection;
        private readonly FigureList _landFigures;
        private readonly VisualElement _metricsSection;
        private readonly FigureList _metricFigures;
        private readonly Button _openScene;
        private readonly Button _recapture;

        private MapSpecDocument _document;
        private MapReport _preview;
        private MapReport _build;
        private MapReport _issues;

        public MapReportPanel()
        {
            EditorAssets.Layout(typeof(MapReportPanel)).CloneTree(this);
            styleSheets.Add(EditorAssets.Sheet("Issues"));
            _source = this.Require<Label>("source");
            _empty = this.Require<Label>("empty");
            _report = this.Require<VisualElement>("report");
            _issuesSection = this.Require<Section>("issues");
            _issueRows = this.Require<VisualElement>("issue-rows");
            _noIssues = this.Require<Label>("no-issues");
            _landSection = this.Require<VisualElement>("land");
            _landFigures = this.Require<FigureList>("land-figures");
            _metricsSection = this.Require<VisualElement>("metrics");
            _metricFigures = this.Require<FigureList>("metric-figures");
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
            _preview = RequireFeatures(MapReport.LoadPreview(_document.Spec.name), _document.Spec.name);
            _build = RequireFeatures(MapReport.Load(_document.Spec.name), _document.Spec.name);
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

        private static MapReport RequireFeatures(MapReport report, string map)
        {
            if (report != null && (report.errorFeatures.Count != report.errors.Count ||
                                   report.warningFeatures.Count != report.warnings.Count))
            {
                throw new InvalidOperationException(
                    $"The report of map '{map}' lacks the feature of some issues (errorFeatures or warningFeatures). Preview or build the map again.");
            }

            return report;
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
            _issuesSection.Title = $"Issues ({report.errors.Count} errors, {report.warnings.Count} warnings)";
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
            var figures = land.shares.ConvertAll(share => (share.kind, Percent(share.share)));
            figures.Add(("Fields", Count(land.fields)));
            figures.Add(("Trees", Count(land.trees)));
            figures.Add(("Placements", Count(land.placements)));
            figures.Add(("Decals", Count(land.decals)));
            figures.Add(("Wet decals", Count(land.wetDecals)));
            figures.Add(("Entities", Count(land.entities)));
            figures.Add(("Crop tiles", Count(land.cropTiles)));
            figures.Add(("Crop plants", land.cropPlants.ToString("N0", CultureInfo.InvariantCulture)));
            _landFigures.Set(figures);
        }

        /// <summary>What a build measured: rush times, attack paths, view contrast and each base's buildable ground and resources.</summary>
        private void ShowMetrics(MapMetrics metrics)
        {
            var figures = new List<(string, string)>
            {
                ("Rush", $"{Seconds(metrics.minRush)} to {Seconds(metrics.maxRush)}"),
                ("Attack paths", Count(metrics.paths)),
                ("Path search", $"{metrics.maxExpansions:N0} cells"),
                ("Renewable fields", $"{metrics.renewableFieldsPerPlayer:0.#} per player"),
                ("View contrast", $"spread {metrics.viewLuminanceSpread:0.000}, local {metrics.viewLocalContrast:0.000}"),
            };
            foreach (var spawn in metrics.spawns)
            {
                var text = $"team {spawn.team}, rush {Seconds(spawn.rush)}, buildable {Percent(spawn.buildable)}, "
                    + $"{spawn.finiteFields} finite fields ({spawn.finiteSupply:N0}), "
                    + $"{spawn.renewableFields} renewable ({spawn.renewableSupply:N0})";
                figures.Add(($"Slot {spawn.slot}", text));
            }

            _metricFigures.Set(figures);
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
