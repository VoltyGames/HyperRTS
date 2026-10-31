using System.Collections.Generic;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Hub;
using HyperRTS.Presentation.HUD;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Validation
{
    /// <summary>Tools ▸ Validate: lists setup problems in prefabs and open scenes; click one to select it.</summary>
    public class ValidatePage : HubPage
    {
        private const string SelectionOnlyKey = "HyperRTS.Validate.SelectionOnly";
        private const string NoProblems = "No problems found. Closed SubScenes aren't checked: open them to include their content.";

        private List<ValidationIssue> _issues = new();
        private Label _counts;
        private HelpBox _empty;
        private IssueList _list;
        private ToolbarToggle _selectionOnly;

        public override string Title => "Validate";

        public override int Order => 30;

        /// <summary>Errors and warnings from the last validation as "3E 12W", or null when there were none.</summary>
        public override string Badge
        {
            get
            {
                var errors = Count(MessageType.Error);
                var warnings = Count(MessageType.Warning);
                if (errors == 0 && warnings == 0)
                {
                    return null;
                }

                if (warnings == 0)
                {
                    return $"{errors}E";
                }

                return errors == 0 ? $"{warnings}W" : $"{errors}E {warnings}W";
            }
        }

        public override VisualElement Build()
        {
            var root = EditorAssets.Layout(typeof(ValidatePage)).Instantiate();
            root.Require<ToolbarButton>("validate").clicked += Refresh;
            _counts = root.Require<Label>("counts");
            _empty = root.Require<HelpBox>("empty");
            _list = root.Require<IssueList>("issues");
            _list.Fixed += Refresh;
            _selectionOnly = root.Require<ToolbarToggle>("selection-only");
            _selectionOnly.SetValueWithoutNotify(EditorPrefs.GetBool(SelectionOnlyKey));
            _selectionOnly.RegisterValueChangedCallback(change =>
            {
                EditorPrefs.SetBool(SelectionOnlyKey, change.newValue);
                Show();
            });
            Selection.selectionChanged += OnSelectionChanged;
            Refresh();
            return root;
        }

        public override void Release()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            _selectionOnly = null;
            _list = null;
            _counts = null;
            _empty = null;
        }

        private void Refresh()
        {
            _issues = ProjectValidator.Run();
            if (_list == null)
            {
                return;
            }

            _counts.text = $"{Count(MessageType.Error)} errors · {Count(MessageType.Warning)} warnings · " +
                           $"{Count(MessageType.Info)} notes";
            Show();
        }

        private void OnSelectionChanged()
        {
            if (_selectionOnly != null && _selectionOnly.value)
            {
                Show();
            }
        }

        private void Show()
        {
            if (_list == null)
            {
                return;
            }

            var shown = _selectionOnly.value ? _issues.FindAll(issue => OnSelection(issue.Context)) : _issues;
            _empty.text = _issues.Count == 0 ? NoProblems : "No problems on the selection.";
            _empty.SetHidden(shown.Count > 0);
            _list.Show(shown);
        }

        // The selected object, anything under it, a component on it, or anything in the same prefab or asset file.
        private static bool OnSelection(Object context)
        {
            if (context == null)
            {
                return false;
            }

            var path = AssetDatabase.GetAssetPath(context);
            var contextObject = Owner(context);
            foreach (var selected in Selection.objects)
            {
                if (context == selected || path.Length > 0 && path == AssetDatabase.GetAssetPath(selected))
                {
                    return true;
                }

                var selectedObject = Owner(selected);
                if (contextObject != null && selectedObject != null &&
                    contextObject.transform.IsChildOf(selectedObject.transform))
                {
                    return true;
                }
            }

            return false;
        }

        private static GameObject Owner(Object target) => target switch
        {
            GameObject gameObject => gameObject,
            Component component => component.gameObject,
            _ => null,
        };

        private int Count(MessageType severity) => _issues.FindAll(issue => issue.Severity == severity).Count;
    }
}
