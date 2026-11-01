using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Presentation.HUD;
using UnityEditor;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Validation
{
    /// <summary>Validation issues as help boxes with their quick-fix buttons; used by inspectors and the validator.</summary>
    [UxmlElement]
    public sealed partial class IssueList : VisualElement
    {
        private readonly HashSet<string> _collapsed = new();
        private readonly Dictionary<int, string> _rules = new();
        private readonly VisualTreeAsset _row = EditorAssets.Layout(typeof(IssueList), "Row");
        private TreeView _tree;

        public IssueList() => styleSheets.Add(EditorAssets.Sheet("Issues"));

        /// <summary>Runs after a quick fix has settled.</summary>
        public event Action Fixed;

        /// <summary>Gives each issue a button that names and selects its object.</summary>
        [UxmlAttribute]
        public bool ShowContext { get; set; }

        /// <summary>Groups issues by rule, then by asset, in a tree whose rules are headed by their counts.</summary>
        [UxmlAttribute]
        public bool Grouped { get; set; }

        public void Show(IReadOnlyList<ValidationIssue> issues)
        {
            if (Grouped)
            {
                ShowTree(issues);
                return;
            }

            Clear();
            foreach (var issue in issues)
            {
                var row = MakeRow();
                BindRow(row, Node.ForIssue(issue));
                Add(row);
            }
        }

        private void ShowTree(IReadOnlyList<ValidationIssue> issues)
        {
            var tree = Tree();
            var id = 0;
            var roots = new List<TreeViewItemData<Node>>();
            var assetIds = new List<int>();
            _rules.Clear();
            var rules = issues.GroupBy(issue => issue.Rule)
                .OrderByDescending(rule => rule.Count(issue => issue.Severity == MessageType.Error))
                .ThenBy(rule => rule.Key);
            foreach (var rule in rules)
            {
                var assets = new List<TreeViewItemData<Node>>();
                foreach (var asset in rule.GroupBy(issue => issue.Context).OrderBy(asset => AssetName(asset.Key)))
                {
                    var rows = asset.Select(issue => new TreeViewItemData<Node>(id++, Node.ForIssue(issue))).ToList();
                    assetIds.Add(id);
                    assets.Add(new TreeViewItemData<Node>(id++, Node.ForAsset(asset.Key), rows));
                }

                _rules[id] = rule.Key;
                roots.Add(new TreeViewItemData<Node>(id++, Node.ForRule($"{rule.Key}  {CountText(rule)}"), assets));
            }

            tree.SetRootItems(roots);
            tree.Rebuild();
            assetIds.ForEach(asset => tree.ExpandItem(asset, false, false));
            foreach (var ruleId in _rules.Where(rule => !_collapsed.Contains(rule.Value)).Select(rule => rule.Key))
            {
                tree.ExpandItem(ruleId, false, false);
            }

            tree.RefreshItems();
        }

        private TreeView Tree()
        {
            if (_tree != null)
            {
                return _tree;
            }

            _tree = new TreeView
            {
                makeItem = MakeRow,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                selectionType = SelectionType.None,
            };
            _tree.bindItem = (row, index) => BindRow(row, _tree.GetItemDataForIndex<Node>(index));
            _tree.itemExpandedChanged += Remember;
            _tree.AddToClassList("hrts-fill");
            AddToClassList("hrts-fill");
            Add(_tree);
            return _tree;
        }

        private void Remember(TreeViewExpansionChangedArgs change)
        {
            if (!_rules.TryGetValue(change.id, out var rule))
            {
                return;
            }

            if (change.isExpanded)
            {
                _collapsed.Remove(rule);
            }
            else
            {
                _collapsed.Add(rule);
            }
        }

        // Clicks read the row's node, so a recycled tree row needs no new handlers.
        private VisualElement MakeRow()
        {
            var row = _row.CloneRoot();
            row.Require<Button>("asset").clicked += () => EditorAssets.Reveal(((Node)row.userData).Asset);
            row.Require<Button>("context").clicked += () => EditorAssets.Reveal(((Node)row.userData).Issue.Context);
            row.Require<Button>("fix").clicked += () => RunFix(((Node)row.userData).Issue.Fix);
            return row;
        }

        private void BindRow(VisualElement row, Node node)
        {
            row.userData = node;
            var rule = row.Require<Label>("rule");
            rule.text = node.Rule;
            rule.SetHidden(node.Rule == null);
            var asset = row.Require<Button>("asset");
            asset.SetHidden(!node.IsAsset);
            asset.text = AssetName(node.Asset);
            asset.SetEnabled(node.Asset != null);

            var issue = node.Issue;
            var context = row.Require<Button>("context");
            context.SetHidden(!node.IsIssue || !ShowContext);
            context.text = issue.Context != null ? issue.Context.name : "-";
            context.SetEnabled(issue.Context != null);
            var message = row.Require<HelpBox>("message");
            message.SetHidden(!node.IsIssue);
            message.text = issue.Message;
            message.messageType = Kind(issue.Severity);
            var fix = row.Require<Button>("fix");
            fix.SetHidden(issue.Fix == null);
            fix.text = issue.FixLabel;
        }

        private static string AssetName(Object context) => context != null ? context.name : "project";

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

        /// <summary>One tree row: a rule heading, an asset heading or an issue.</summary>
        private sealed class Node
        {
            private Node(string rule, bool isAsset, Object asset, bool isIssue, ValidationIssue issue)
            {
                Rule = rule;
                IsAsset = isAsset;
                Asset = asset;
                IsIssue = isIssue;
                Issue = issue;
            }

            public string Rule { get; }

            public bool IsAsset { get; }

            public Object Asset { get; }

            public bool IsIssue { get; }

            public ValidationIssue Issue { get; }

            public static Node ForRule(string heading) => new(heading, false, null, false, default);

            public static Node ForAsset(Object asset) => new(null, true, asset, false, default);

            public static Node ForIssue(ValidationIssue issue) => new(null, false, null, true, issue);
        }
    }
}
