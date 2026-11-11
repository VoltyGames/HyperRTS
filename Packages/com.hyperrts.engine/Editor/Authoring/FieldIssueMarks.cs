using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Validation.Issues;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Marks the inspector field an issue names with a severity edge and shows the message under it.</summary>
    public sealed class FieldIssueMarks
    {
        private const string MessageClass = "hrts-field-issue";

        private readonly VisualElement _root;
        private readonly List<VisualElement> _marked = new();

        public FieldIssueMarks(VisualElement root)
        {
            _root = root;
        }

        /// <summary>Replaces the marks with those of <paramref name="issues"/>; issues without a path are left to the badge.</summary>
        public void Show(IEnumerable<ValidationIssue> issues)
        {
            Clear();
            var drawn = new Dictionary<string, PropertyField>();
            _root.Query<PropertyField>().ForEach(field =>
            {
                if (!string.IsNullOrEmpty(field.bindingPath))
                {
                    drawn.TryAdd(field.bindingPath, field);
                }
            });
            foreach (var onField in issues.Where(issue => !string.IsNullOrEmpty(issue.Path)).GroupBy(issue => issue.Path))
            {
                var field = FindField(drawn, onField.Key);
                if (field != null)
                {
                    Mark(field, onField.ToList());
                }
            }
        }

        // A path into a list entry or a block that is not drawn (yet) marks the nearest field that holds it.
        private static PropertyField FindField(Dictionary<string, PropertyField> drawn, string path)
        {
            while (path.Length > 0)
            {
                if (drawn.TryGetValue(path, out var field))
                {
                    return field;
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
            message.AddToClassList(MessageClass);
            message.AddToClassList(isError ? "hrts-field-issue--error" : "hrts-field-issue--warning");
            field.parent.Insert(field.parent.IndexOf(field) + 1, message);
        }

        private void Clear()
        {
            foreach (var field in _marked)
            {
                field.RemoveFromClassList("hrts-field--error");
                field.RemoveFromClassList("hrts-field--warning");
            }

            _root.Query(className: MessageClass).ToList().ForEach(message => message.RemoveFromHierarchy());
            _marked.Clear();
        }
    }
}
