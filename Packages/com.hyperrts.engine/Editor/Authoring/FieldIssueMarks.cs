using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Validation;
using UnityEditor;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Marks the inspector field an issue names with a severity edge and shows the message under it.</summary>
    public sealed class FieldIssueMarks
    {
        private readonly VisualElement _root;
        private readonly List<VisualElement> _marked = new();
        private readonly List<VisualElement> _messages = new();

        public FieldIssueMarks(VisualElement root)
        {
            _root = root;
        }

        /// <summary>Replaces the marks with those of <paramref name="issues"/>; issues without a path are left to the badge.</summary>
        public void Show(IEnumerable<ValidationIssue> issues)
        {
            Clear();
            foreach (var onField in issues.Where(issue => !string.IsNullOrEmpty(issue.Path)).GroupBy(issue => issue.Path))
            {
                var field = FindField(onField.Key);
                if (field != null)
                {
                    Mark(field, onField.ToList());
                }
            }
        }

        // A path into a list entry or a block that is not drawn (yet) marks the nearest field that holds it.
        private PropertyField FindField(string path)
        {
            while (path.Length > 0)
            {
                var drawn = _root.Query<PropertyField>().Where(field => field.bindingPath == path).First();
                if (drawn != null)
                {
                    return drawn;
                }

                path = path.Substring(0, Mathf.Max(path.LastIndexOf('.'), 0));
            }

            return null;
        }

        private void Mark(PropertyField field, List<ValidationIssue> issues)
        {
            var isError = issues.Any(issue => issue.Severity == MessageType.Error);
            field.AddToClassList(isError ? "hrts-field--error" : "hrts-field--warning");
            _marked.Add(field);

            var message = new Label(string.Join("\n", issues.Select(issue => issue.Message).Distinct()));
            message.AddToClassList("hrts-field-issue");
            message.AddToClassList(isError ? "hrts-field-issue--error" : "hrts-field-issue--warning");
            field.parent.Insert(field.parent.IndexOf(field) + 1, message);
            _messages.Add(message);
        }

        private void Clear()
        {
            foreach (var field in _marked)
            {
                field.RemoveFromClassList("hrts-field--error");
                field.RemoveFromClassList("hrts-field--warning");
            }

            foreach (var message in _messages)
            {
                message.RemoveFromHierarchy();
            }

            _marked.Clear();
            _messages.Clear();
        }
    }
}
