using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Authoring;
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
                ShowTree(issues.Select(issue => new IssueGroup(issue)).ToList());
                return;
            }

            Clear();
            foreach (var issue in issues)
            {
                var row = MakeRow();
                BindRow(row, Node.ForIssue(new IssueGroup(issue)));
                Add(row);
            }
        }

        /// <summary>Shows the groups as the rule and asset tree, with each group's variants under its row.</summary>
        public void Show(IReadOnlyList<IssueGroup> groups) => ShowTree(groups);

        private void ShowTree(IReadOnlyList<IssueGroup> groups)
        {
            var tree = Tree();
            var id = 0;
            var roots = new List<TreeViewItemData<Node>>();
            var assetIds = new List<int>();
            _rules.Clear();
            var rules = groups.GroupBy(group => group.Issue.Rule)
                .OrderByDescending(rule => rule.Count(group => group.Issue.Severity == MessageType.Error))
                .ThenBy(rule => rule.Key);
            foreach (var rule in rules)
            {
                var assets = new List<TreeViewItemData<Node>>();
                foreach (var asset in rule.GroupBy(group => group.Issue.Context).OrderBy(asset => AssetName(asset.Key)))
                {
                    var rows = new List<TreeViewItemData<Node>>();
                    foreach (var group in asset)
                    {
                        var variants = group.Variants.Select(issue => new TreeViewItemData<Node>(id++, Node.ForVariant(issue))).ToList();
                        rows.Add(new TreeViewItemData<Node>(id++, Node.ForIssue(group), variants));
                    }

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
            row.RegisterCallback<ClickEvent>(SelectIssue);
            return row;
        }

        // A click on the row itself, not on one of its buttons, selects and pings what the issue is about.
        private void SelectIssue(ClickEvent click)
        {
            var node = (Node)((VisualElement)click.currentTarget).userData;
            var hit = (VisualElement)click.target;
            var onButton = hit is Button || hit.GetFirstAncestorOfType<Button>() != null;
            if (Grouped && node.IsIssue && node.Issue.Context != null && !onButton)
            {
                EditorAssets.Reveal(node.Issue.Context);
            }
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
            context.SetHidden(!node.IsIssue || !ShowContext && !node.IsVariant);
            context.text = issue.Context != null ? issue.Context.name : "-";
            context.SetEnabled(issue.Context != null);
            var message = row.Require<HelpBox>("message");
            message.SetHidden(!node.IsIssue);
            message.text = issue.Message;
            message.messageType = Kind(issue.Severity);
            var rowTip = node.IsIssue && issue.Rule != null ? $"Rule: {issue.Rule}" : "";
            row.tooltip = rowTip;
            message.tooltip = rowTip;
            var variants = row.Require<Label>("variants");
            variants.SetHidden(node.VariantText.Length == 0);
            variants.text = node.VariantText;
            var fix = row.Require<Button>("fix");
            fix.SetHidden(issue.Fix == null);
            fix.text = issue.FixLabel;
        }

        private static string AssetName(Object context) => context != null ? context.name : "project";

        private static string CountText(IEnumerable<IssueGroup> groups)
        {
            var errors = groups.Count(group => group.Issue.Severity == MessageType.Error);
            var warnings = groups.Count(group => group.Issue.Severity == MessageType.Warning);
            var parts = new[] { IssueBadge.CountText(errors, "error"), IssueBadge.CountText(warnings, "warning") };
            return "(" + string.Join(", ", parts.Where(part => part.Length > 0)) + ")";
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
            _ => HelpBoxMessageType.None,
        };

        /// <summary>One tree row: a rule heading, an asset heading, an issue or an issue on a variant.</summary>
        private sealed class Node
        {
            private Node(string rule, bool isAsset, Object asset, bool isIssue, ValidationIssue issue, string variantText, bool isVariant)
            {
                Rule = rule;
                IsAsset = isAsset;
                Asset = asset;
                IsIssue = isIssue;
                Issue = issue;
                VariantText = variantText;
                IsVariant = isVariant;
            }

            public string Rule { get; }

            public bool IsAsset { get; }

            public Object Asset { get; }

            public bool IsIssue { get; }

            public ValidationIssue Issue { get; }

            public string VariantText { get; }

            public bool IsVariant { get; }

            public static Node ForRule(string heading) => new(heading, false, null, false, default, "", false);

            public static Node ForAsset(Object asset) => new(null, true, asset, false, default, "", false);

            public static Node ForIssue(IssueGroup group) => new(null, false, null, true, group.Issue, group.VariantText, false);

            public static Node ForVariant(ValidationIssue issue) => new(null, false, null, true, issue, "", true);
        }
    }
}
