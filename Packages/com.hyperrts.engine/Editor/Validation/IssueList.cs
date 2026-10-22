using System;
using System.Collections.Generic;
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
        private readonly VisualTreeAsset _row = EditorAssets.Layout(typeof(IssueList), "Row");

        /// <summary>Runs after a quick fix has settled.</summary>
        public event Action Fixed;

        /// <summary>Gives each issue a button that names and selects its object.</summary>
        [UxmlAttribute]
        public bool ShowContext { get; set; }

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
