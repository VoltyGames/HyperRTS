using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Presentation.HUD;
using UnityEditor;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Validation
{
    /// <summary>Validation issues as help boxes with their quick-fix buttons; used by inspectors and the validator.</summary>
    [UxmlElement]
    public sealed partial class IssueList : VisualElement
    {
        private readonly HashSet<string> _collapsed = new();
        private readonly VisualTreeAsset _row = EditorAssets.Layout(typeof(IssueList), "Row");

        /// <summary>Runs after a quick fix has settled.</summary>
        public event Action Fixed;

        /// <summary>Gives each issue a button that names and selects its object.</summary>
        [UxmlAttribute]
        public bool ShowContext { get; set; }

        /// <summary>Groups issues by rule, then by asset, each rule in a foldout headed by its counts.</summary>
        [UxmlAttribute]
        public bool Grouped { get; set; }

        public void Show(IReadOnlyList<ValidationIssue> issues)
        {
            Clear();
            if (Grouped)
            {
                ShowGrouped(issues);
                return;
            }

            foreach (var issue in issues)
            {
                Add(Row(issue));
            }
        }

        private void ShowGrouped(IReadOnlyList<ValidationIssue> issues)
        {
            var rules = issues.GroupBy(issue => issue.Rule)
                .OrderByDescending(rule => rule.Count(issue => issue.Severity == MessageType.Error))
                .ThenBy(rule => rule.Key);
            foreach (var rule in rules)
            {
                var name = rule.Key;
                var foldout = new Foldout { text = $"{name}  {CountText(rule)}", value = !_collapsed.Contains(name) };
                foldout.AddToClassList("hrts-issue-group");
                foldout.RegisterValueChangedCallback(change =>
                {
                    if (change.target != foldout)
                    {
                        return;
                    }

                    if (change.newValue)
                    {
                        _collapsed.Remove(name);
                    }
                    else
                    {
                        _collapsed.Add(name);
                    }
                });
                AddAssets(foldout, rule);
                Add(foldout);
            }
        }

        private void AddAssets(VisualElement foldout, IEnumerable<ValidationIssue> ruleIssues)
        {
            foreach (var asset in ruleIssues.GroupBy(issue => issue.Context).OrderBy(asset => AssetName(asset.Key)))
            {
                var context = asset.Key;
                var heading = new Button(() => EditorAssets.Reveal(context)) { text = AssetName(context) };
                heading.AddToClassList("hrts-issue-group__asset");
                heading.SetEnabled(context != null);
                foldout.Add(heading);

                var rows = new VisualElement();
                rows.AddToClassList("hrts-issue-group__issues");
                foreach (var issue in asset)
                {
                    rows.Add(Row(issue));
                }

                foldout.Add(rows);
            }
        }

        private static string AssetName(UnityEngine.Object context) => context != null ? context.name : "project";

        private static string CountText(IEnumerable<ValidationIssue> issues)
        {
            var errors = issues.Count(issue => issue.Severity == MessageType.Error);
            var warnings = issues.Count(issue => issue.Severity == MessageType.Warning);
            var notes = issues.Count() - errors - warnings;
            var parts = new List<string>();
            if (errors > 0)
            {
                parts.Add($"{errors} errors");
            }

            if (warnings > 0)
            {
                parts.Add($"{warnings} warnings");
            }

            if (notes > 0)
            {
                parts.Add($"{notes} notes");
            }

            return "(" + string.Join(", ", parts) + ")";
        }

        private VisualElement Row(ValidationIssue issue)
        {
            var row = _row.CloneRoot();
            var context = row.Require<Button>("context");
            context.SetHidden(!ShowContext);
            context.text = issue.Context != null ? issue.Context.name : "-";
            context.clicked += () => EditorAssets.Reveal(issue.Context);
            context.SetEnabled(issue.Context != null);

            var message = row.Require<HelpBox>("message");
            message.text = issue.Message;
            message.messageType = Kind(issue.Severity);

            var fix = row.Require<Button>("fix");
            fix.SetHidden(issue.Fix == null);
            fix.text = issue.FixLabel;
            fix.clicked += () => RunFix(issue.Fix);
            return row;
        }

        // A fix can add, remove or replace objects, so re-validate once it has settled.
        private void RunFix(Action fix)
        {
            fix();
            EditorApplication.delayCall += () => Fixed?.Invoke();
        }

        private static HelpBoxMessageType Kind(MessageType severity) => severity switch
        {
            MessageType.Error => HelpBoxMessageType.Error,
            MessageType.Warning => HelpBoxMessageType.Warning,
            MessageType.Info => HelpBoxMessageType.Info,
            _ => HelpBoxMessageType.None,
        };
    }
}
