using System;
using System.Collections.Generic;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Maps.Common;
using HyperRTS.Editor.Maps.Report;
using HyperRTS.Presentation.HUD;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// The Map Editor's Issues tab: the newest preview's or build's errors then warnings. Clicking one that names a
    /// feature frames it on the canvas; one about the map as a whole only lists.
    /// </summary>
    [UxmlElement]
    public sealed partial class MapIssueList : VisualElement
    {
        private const string LocatedClass = "hrts-map-issue--located";

        private readonly VisualTreeAsset _row = EditorAssets.Layout(typeof(MapIssueList), "Issue");
        private readonly Label _summary;
        private readonly Label _empty;
        private readonly Label _none;
        private readonly VisualElement _rows;

        public MapIssueList()
        {
            EditorAssets.Layout(typeof(MapIssueList)).CloneTree(this);
            _summary = this.Require<Label>("summary");
            _empty = this.Require<Label>("empty");
            _none = this.Require<Label>("none");
            _rows = this.Require<VisualElement>("rows");
            Show(null);
        }

        /// <summary>An issue with a location was clicked; true when it is an error, false for a warning.</summary>
        public event Action<MapIssueFeature, bool> Picked;

        /// <summary>The number of errors and warnings shown, for the tab label.</summary>
        public int Count { get; private set; }

        public event Action Changed;

        /// <summary>Lists <paramref name="report"/>'s issues, or the empty note for null.</summary>
        public void Show(MapReport report)
        {
            _rows.Clear();
            _empty.SetHidden(report != null);
            Count = report == null ? 0 : report.errors.Count + report.warnings.Count;
            _none.SetHidden(report == null || Count > 0);
            _summary.text = report == null ? "Issues" : $"{report.errors.Count} errors, {report.warnings.Count} warnings";
            if (report != null)
            {
                AddRows(report.errors, report.errorFeatures, true);
                AddRows(report.warnings, report.warningFeatures, false);
            }

            Changed?.Invoke();
        }

        private void AddRows(List<string> messages, List<MapIssueFeature> features, bool error)
        {
            for (var i = 0; i < messages.Count; i++)
            {
                var feature = features[i];
                var row = _row.CloneRoot();
                var message = row.Require<HelpBox>("message");
                message.text = messages[i];
                message.messageType = error ? HelpBoxMessageType.Error : HelpBoxMessageType.Warning;
                if (!feature.IsNone)
                {
                    row.AddToClassList(LocatedClass);
                    row.tooltip = $"Select and frame {feature}";
                    row.RegisterCallback<ClickEvent>(_ => Picked?.Invoke(feature, error));
                }

                _rows.Add(row);
            }
        }
    }
}
