using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Validation;
using HyperRTS.Presentation.HUD;
using UnityEditor;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Issue counts as a header badge (red errors, yellow warnings) that expands the issue list on click.</summary>
    [UxmlElement]
    public sealed partial class IssueBadge : VisualElement
    {
        private readonly Button _badge;
        private readonly Label _errors;
        private readonly Label _warnings;
        private readonly Label _notes;
        private readonly IssueList _list;
        private bool _expanded;

        public IssueBadge()
        {
            EditorAssets.Layout(typeof(IssueBadge)).CloneTree(this);
            _badge = this.Require<Button>("badge");
            _badge.clicked += Toggle;
            _errors = this.Require<Label>("errors");
            _warnings = this.Require<Label>("warnings");
            _notes = this.Require<Label>("notes");
            _list = this.Require<IssueList>("list");
            _list.Fixed += () => Fixed?.Invoke();
        }

        /// <summary>Runs after a quick fix has settled.</summary>
        public event Action Fixed;

        public void Show(IReadOnlyList<ValidationIssue> issues)
        {
            this.SetHidden(issues.Count == 0);
            SetCount(_errors, Tally(issues, MessageType.Error), "error");
            SetCount(_warnings, Tally(issues, MessageType.Warning), "warning");
            SetCount(_notes, Tally(issues, MessageType.Info), "note");
            _list.Show(issues);
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
            _list.SetHidden(!_expanded);
            _badge.EnableInClassList("hrts-issue-badge__button--open", _expanded);
        }

        private static int Tally(IReadOnlyList<ValidationIssue> issues, MessageType severity) =>
            issues.Count(issue => issue.Severity == severity);

        private static void SetCount(Label label, int count, string noun)
        {
            label.text = CountText(count, noun);
            label.SetHidden(count == 0);
        }
    }
}
