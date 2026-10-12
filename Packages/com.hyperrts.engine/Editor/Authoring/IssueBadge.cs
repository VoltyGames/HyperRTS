using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Validation;
using UnityEditor;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Issue counts as a header badge (red errors, yellow warnings) that expands the issue list on click.</summary>
    public sealed class IssueBadge : VisualElement
    {
        private readonly Button _badge;
        private readonly Label _errors;
        private readonly Label _warnings;
        private readonly Label _notes;
        private readonly IssueList _list;
        private bool _expanded;

        /// <summary><paramref name="fixed"/> runs after a quick fix.</summary>
        public IssueBadge(Action @fixed)
        {
            AddToClassList("hrts-issue-badge");
            _badge = new Button(Toggle);
            _badge.AddToClassList("hrts-issue-badge__button");
            _errors = Count(_badge, "hrts-issue-badge__errors");
            _warnings = Count(_badge, "hrts-issue-badge__warnings");
            _notes = Count(_badge, "hrts-issue-badge__notes");
            Add(_badge);

            _list = new IssueList(@fixed, showContext: false);
            Add(_list);
        }

        public void Show(IReadOnlyList<ValidationIssue> issues)
        {
            style.display = issues.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            SetCount(_errors, Tally(issues, MessageType.Error), "error");
            SetCount(_warnings, Tally(issues, MessageType.Warning), "warning");
            SetCount(_notes, Tally(issues, MessageType.Info), "note");
            _list.Show(issues);
            _list.style.display = _expanded ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Plural label such as "2 errors"; empty for none.</summary>
        public static string CountText(int count, string noun) => count switch
        {
            0 => "",
            1 => "1 " + noun,
            _ => $"{count} {noun}s",
        };

        private void Toggle()
        {
            _expanded = !_expanded;
            _list.style.display = _expanded ? DisplayStyle.Flex : DisplayStyle.None;
            _badge.EnableInClassList("hrts-issue-badge__button--open", _expanded);
        }

        private static Label Count(VisualElement parent, string className)
        {
            var label = new Label();
            label.AddToClassList("hrts-issue-badge__count");
            label.AddToClassList(className);
            parent.Add(label);
            return label;
        }

        private static int Tally(IReadOnlyList<ValidationIssue> issues, MessageType severity) =>
            issues.Count(issue => issue.Severity == severity);

        private static void SetCount(Label label, int count, string noun)
        {
            label.text = CountText(count, noun);
            label.style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
