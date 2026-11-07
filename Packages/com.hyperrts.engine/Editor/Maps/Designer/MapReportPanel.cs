using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Maps.Build;
using HyperRTS.Editor.Maps.Report;
using HyperRTS.Editor.Maps.Views;
using HyperRTS.Presentation.HUD;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// The Map Editor's Report tab: the newest preview's or build's land figures and the last build's metrics, with
    /// Open scene and Recapture views. Its issues go to the Issues tab.
    /// </summary>
    [UxmlElement]
    public sealed partial class MapReportPanel : VisualElement
    {
        private readonly Label _source;
        private readonly Label _empty;
        private readonly VisualElement _report;
        private readonly VisualElement _landSection;
        private readonly ValueList _landFigures;
        private readonly VisualElement _metricsSection;
        private readonly ValueList _metricFigures;
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
            _landSection = this.Require<VisualElement>("land");
            _landFigures = this.Require<ValueList>("land-figures");
            _metricsSection = this.Require<VisualElement>("metrics");
            _metricFigures = this.Require<ValueList>("metric-figures");
            _openScene = this.Require<Button>("open-scene");
            _openScene.clicked += OpenScene;
            _recapture = this.Require<Button>("recapture");
            _recapture.clicked += Recapture;
        }

        /// <summary>The newest preview's or build's report, or null before either.</summary>
        public MapReport Issues => _issues;

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
            var built = File.Exists(new MapPaths(_document.Path, _document.Spec.name).Scene);
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
            if (showLand)
            {
                ShowLand(land);
            }

            if (showMetrics)
            {
                ShowMetrics(_build.metrics);
            }
        }

        /// <summary>Land use shares and what stands on the land, from a preview or a build.</summary>
        private void ShowLand(MapLandShares land)
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
            var composition = land.composition;
            figures.Add(("Composed frames", $"{Percent(composition.share)} of {composition.frames}"));
            figures.Add(("Frames with", $"wood edge {Percent(composition.woodEdgeShare)}, water {Percent(composition.waterShare)}, "
                + $"fields {Percent(composition.fieldPatternShare)}, settlement {Percent(composition.settlementShare)}, "
                + $"relief {Percent(composition.reliefShare)}"));
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

        private void OpenScene() => new MapPaths(_document.Path, _document.Spec.name).OpenScene();

        private void Recapture()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                MapViewCapture.Capture(_document.Path);
            }
        }
    }
}
