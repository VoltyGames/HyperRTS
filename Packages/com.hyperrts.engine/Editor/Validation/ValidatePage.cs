using System.Collections.Generic;
using HyperRTS.Editor.Common;
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
            var root = EditorAssets.Layout(typeof(ValidatePage)).Instantiate();
            root.Require<ToolbarButton>("validate").clicked += Refresh;
            _counts = root.Require<Label>("counts");
            _empty = root.Require<HelpBox>("empty");
            _list = root.Require<IssueList>("issues");
            _list.Fixed += Refresh;
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
            _empty.EnableInClassList("hrts-hidden", _issues.Count > 0);
            _list.Show(_issues);
        }

        private int Count(MessageType severity) => _issues.FindAll(issue => issue.Severity == severity).Count;
    }
}
