using System.Collections.Generic;
using HyperRTS.Editor.Common;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Validation
{
    /// <summary>HyperRTS ▸ Validate: lists setup problems in prefabs and open scenes; click one to select it.</summary>
    public class ValidatorWindow : EditorWindow
    {
        private List<ValidationIssue> _issues = new();
        private Label _counts;
        private HelpBox _empty;
        private IssueList _list;

        [MenuItem(EditorMenu.Validate, false, EditorMenu.ValidatePriority)]
        public static void Open() => GetWindow<ValidatorWindow>("HyperRTS Validator").Refresh();

        public void CreateGUI()
        {
            var root = rootVisualElement;
            EditorAssets.AddStyles(root);

            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(Refresh) { text = "Validate" });
            toolbar.Add(new ToolbarSpacer { flex = true });
            _counts = new Label();
            _counts.AddToClassList("hrts-toolbar-label");
            toolbar.Add(_counts);
            root.Add(toolbar);

            _empty = new HelpBox("No problems found. Closed SubScenes aren't checked: open them to include their " +
                                 "content.", HelpBoxMessageType.Info);
            _empty.AddToClassList("hrts-note");
            root.Add(_empty);

            var scroll = new ScrollView();
            scroll.AddToClassList("hrts-fill");
            _list = new IssueList(Refresh, showContext: true);
            scroll.Add(_list);
            root.Add(scroll);
            Refresh();
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
            _empty.style.display = _issues.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _list.Show(_issues);
        }

        private int Count(MessageType severity) => _issues.FindAll(issue => issue.Severity == severity).Count;
    }
}
