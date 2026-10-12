using System.Collections.Generic;
using HyperRTS.Editor.Hub;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Validation
{
    /// <summary>Tools ▸ Validate: lists setup problems in prefabs and open scenes; click one to select it.</summary>
    public class ValidatePage : HubPage
    {
        private List<ValidationIssue> _issues = new();
        private Label _counts;
        private HelpBox _empty;
        private IssueList _list;

        public override string Title => "Validate";

        public override int Order => 30;

        /// <summary>Errors found by the last validation, or null when there were none.</summary>
        public override string Badge
        {
            get
            {
                var errors = Count(MessageType.Error);
                return errors > 0 ? errors.ToString() : null;
            }
        }

        public override VisualElement Build()
        {
            var root = new VisualElement();
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
            return root;
        }

        public override void Release()
        {
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
            _empty.style.display = _issues.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _list.Show(_issues);
        }

        private int Count(MessageType severity) => _issues.FindAll(issue => issue.Severity == severity).Count;
    }
}
