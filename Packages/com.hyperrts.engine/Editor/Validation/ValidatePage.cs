using System.Collections.Generic;
using HyperRTS.Editor.Authoring;
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
        private const string NoProblems = "No problems found. Closed SubScenes aren't checked: open them to include their content.";

        private IssueGroups _groups = new();
        private IssueGroups _shown = new();
        private string _search = "";
        private ToolbarButton _errors;
        private ToolbarButton _warnings;
        private ToolbarButton _fixAll;
        private PageShell _shell;
        private IssueList _list;

        public override string Title => "Validate";

        public override string Glyph => "shield-check";

        public override int Order => 30;

        /// <summary>Errors and warnings from the last validation.</summary>
        public override PageBadge Badge
        {
            get
            {
                var errors = SessionState.GetInt(ProjectValidator.ErrorsKey, 0);
                var warnings = SessionState.GetInt(ProjectValidator.WarningsKey, 0);
                return new PageBadge(errors + warnings, errors > 0);
            }
        }

        public override VisualElement Build()
        {
            var root = Layout().Instantiate();
            EditorState.Bind(root);
            root.Require<ToolbarButton>("validate").clicked += Refresh;
            _errors = root.Require<ToolbarButton>("errors");
            _errors.clicked += () => Toggle(ref EditorState.instance.validateErrors);
            _warnings = root.Require<ToolbarButton>("warnings");
            _warnings.clicked += () => Toggle(ref EditorState.instance.validateWarnings);
            _fixAll = root.Require<ToolbarButton>("fix-all");
            _fixAll.clicked += FixAll;
            root.Require<ToolbarSearchField>("search").RegisterValueChangedCallback(change =>
            {
                _search = change.newValue;
                Show(SelectionOnly);
            });
            _shell = root.Require<PageShell>("shell");
            _list = root.Require<IssueList>("issues");
            _list.Fixed += Refresh;
            root.Require<ToolbarToggle>("selection-only").RegisterValueChangedCallback(change => Show(change.newValue));
            root.WhileAttached(() => Selection.selectionChanged += OnSelectionChanged,
                () => Selection.selectionChanged -= OnSelectionChanged);
            Refresh();
            return root;
        }

        private static bool SelectionOnly => EditorState.instance.validateSelectionOnly;

        private void Refresh()
        {
            _groups = ProjectValidator.Run().GroupVariants();
            Show(SelectionOnly);
        }

        private void Toggle(ref bool shown)
        {
            shown = !shown;
            EditorState.instance.Save();
            Show(SelectionOnly);
        }

        private void FixAll()
        {
            _shown.FixAll();
            EditorApplication.delayCall += Refresh;
        }

        private void OnSelectionChanged()
        {
            if (SelectionOnly)
            {
                Show(true);
            }
        }

        // The toggle passes its new value, since the binding may write the state after this callback runs.
        private void Show(bool selectionOnly)
        {
            var scoped = _groups.Filter(group => (!selectionOnly || OnSelection(group.Issue.Context)) && group.Matches(_search));
            var state = EditorState.instance;
            _shown = scoped.Filter(group => group.Issue.Severity == MessageType.Error ? state.validateErrors : state.validateWarnings);
            ShowChip(_errors, "errors-count", state.validateErrors, scoped.Tally(MessageType.Error), "error");
            ShowChip(_warnings, "warnings-count", state.validateWarnings, scoped.Tally(MessageType.Warning), "warning");
            _fixAll.SetEnabled(_shown.HasFix);
            _shell.EmptyText = EmptyText(selectionOnly);
            _shell.ShowEmpty(_shown.Count == 0);
            _list.Show(_shown);
        }

        private string EmptyText(bool selectionOnly)
        {
            if (_groups.Count == 0)
            {
                return NoProblems;
            }

            return selectionOnly ? "No problems on the selection." : "No problems match the filters.";
        }

        private static void ShowChip(ToolbarButton chip, string countName, bool on, int count, string noun)
        {
            chip.Require<Label>(countName).text = count.ToString();
            chip.tooltip = (on ? "Hide " : "Show ") + noun + "s: " + (count == 0 ? "none" : IssueBadge.CountText(count, noun));
            chip.EnableInClassList("hrts-severity-chip--off", !on);
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
    }
}
