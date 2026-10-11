using System;
using System.Collections.Generic;
using HyperRTS.Editor.Common;
using UnityEditor;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Validation
{
    /// <summary>Validation issues as help boxes with their quick-fix buttons; used by inspectors and the validator.</summary>
    public sealed class IssueList : VisualElement
    {
        private readonly Action _fixed;
        private readonly bool _showContext;

        /// <summary><paramref name="fixed"/> runs after a quick fix; context buttons name and select each issue's object.</summary>
        public IssueList(Action @fixed, bool showContext)
        {
            _fixed = @fixed;
            _showContext = showContext;
        }

        public void Show(IReadOnlyList<ValidationIssue> issues)
        {
            Clear();
            foreach (var issue in issues)
            {
                Add(Row(issue));
            }
        }

        private VisualElement Row(ValidationIssue issue)
        {
            var row = new VisualElement();
            row.AddToClassList("hrts-issue");
            if (_showContext)
            {
                row.Add(ContextButton(issue));
            }

            var message = new HelpBox(issue.Message, Kind(issue.Severity));
            message.AddToClassList("hrts-issue__message");
            row.Add(message);

            if (issue.Fix != null)
            {
                var fix = new Button(() => RunFix(issue.Fix)) { text = issue.FixLabel };
                fix.AddToClassList("hrts-issue__fix");
                row.Add(fix);
            }

            return row;
        }

        private static Button ContextButton(ValidationIssue issue)
        {
            var context = issue.Context;
            var button = new Button(() => EditorAssets.Reveal(context)) { text = context != null ? context.name : "-" };
            button.AddToClassList("hrts-issue__context");
            button.SetEnabled(context != null);
            return button;
        }

        // A fix can add, remove or replace objects, so re-validate once it has settled.
        private void RunFix(Action fix)
        {
            fix();
            EditorApplication.delayCall += () => _fixed();
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
