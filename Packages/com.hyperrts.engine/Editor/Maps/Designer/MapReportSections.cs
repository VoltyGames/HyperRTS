using System;
using System.Collections.Generic;
using System.Globalization;
using HyperRTS.Editor.Hub;
using HyperRTS.Editor.Maps.Report;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>The Map Editor report panel's sections: issues with links to their features, land figures and metrics.</summary>
    public static class MapReportSections
    {
        /// <summary>Errors then warnings; issues that name a feature get a Show button that calls <paramref name="show"/>.</summary>
        public static VisualElement Issues(MapReport report, Action<MapIssueFeature> show)
        {
            var section = HubSections.Section($"Issues ({report.errors.Count} errors, {report.warnings.Count} warnings)");
            AddIssues(section, report.errors, report.errorFeatures, HelpBoxMessageType.Error, show);
            AddIssues(section, report.warnings, report.warningFeatures, HelpBoxMessageType.Warning, show);
            if (report.errors.Count + report.warnings.Count == 0)
            {
                section.Add(Note("No issues."));
            }

            return section;
        }

        /// <summary>Land use shares and what stands on the land, from a preview or a build.</summary>
        public static VisualElement Land(MapLandFigures land)
        {
            var section = HubSections.Section("Land");
            foreach (var share in land.shares)
            {
                section.Add(Figure(share.kind, Percent(share.share)));
            }

            section.Add(Figure("Fields", land.fields.ToString(CultureInfo.InvariantCulture)));
            section.Add(Figure("Trees", land.trees.ToString(CultureInfo.InvariantCulture)));
            section.Add(Figure("Placements", land.placements.ToString(CultureInfo.InvariantCulture)));
            section.Add(Figure("Decals", land.decals.ToString(CultureInfo.InvariantCulture)));
            section.Add(Figure("Entities", land.entities.ToString(CultureInfo.InvariantCulture)));
            section.Add(Figure("Crop tiles", land.cropTiles.ToString(CultureInfo.InvariantCulture)));
            section.Add(Figure("Crop plants", land.cropPlants.ToString("N0", CultureInfo.InvariantCulture)));
            return section;
        }

        /// <summary>What a build measured: rush times, attack paths and each base's buildable ground and resources.</summary>
        public static VisualElement Metrics(MapMetrics metrics)
        {
            var section = HubSections.Section("Metrics (last build)");
            section.Add(Figure("Rush", $"{Seconds(metrics.minRush)} to {Seconds(metrics.maxRush)}"));
            section.Add(Figure("Attack paths", metrics.paths.ToString(CultureInfo.InvariantCulture)));
            section.Add(Figure("Path search", $"{metrics.maxExpansions:N0} cells"));
            section.Add(Figure("Renewable fields", $"{metrics.renewableFieldsPerPlayer:0.#} per player"));
            foreach (var spawn in metrics.spawns)
            {
                var text = $"team {spawn.team}, rush {Seconds(spawn.rush)}, buildable {Percent(spawn.buildable)}, "
                    + $"{spawn.finiteFields} finite fields ({spawn.finiteSupply:N0}), "
                    + $"{spawn.renewableFields} renewable ({spawn.renewableSupply:N0})";
                section.Add(Figure($"Slot {spawn.slot}", text));
            }

            return section;
        }

        public static Label Note(string text)
        {
            var note = new Label(text);
            note.AddToClassList("hrts-note");
            return note;
        }

        private static void AddIssues(VisualElement section, List<string> messages, List<MapIssueFeature> features,
            HelpBoxMessageType kind, Action<MapIssueFeature> show)
        {
            for (var i = 0; i < messages.Count; i++)
            {
                section.Add(IssueRow(messages[i], kind, features[i], show));
            }
        }

        private static VisualElement IssueRow(string message, HelpBoxMessageType kind, MapIssueFeature feature,
            Action<MapIssueFeature> show)
        {
            var row = new VisualElement();
            row.AddToClassList("hrts-issue");
            var box = new HelpBox(message, kind);
            box.AddToClassList("hrts-issue__message");
            row.Add(box);
            if (!feature.IsNone)
            {
                var button = new Button(() => show(feature)) { text = "Show", tooltip = $"Select and frame {feature}" };
                button.AddToClassList("hrts-map-report__show");
                row.Add(button);
            }

            return row;
        }

        private static VisualElement Figure(string name, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("hrts-row");
            var label = new Label(name);
            label.AddToClassList("hrts-map-report__name");
            row.Add(label);
            var text = new Label(value);
            text.AddToClassList("hrts-row__grow");
            row.Add(text);
            return row;
        }

        private static string Percent(float share) => share.ToString("P0", CultureInfo.InvariantCulture);

        private static string Seconds(float seconds) => seconds.ToString("0' s'", CultureInfo.InvariantCulture);
    }
}
