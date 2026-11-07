using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace HyperRTS.Editor.Validation
{
    /// <summary>The issue groups a page shows, with the counts and the bulk fix its toolbar needs.</summary>
    public sealed class IssueGroups : List<IssueGroup>
    {
        public IssueGroups()
        {
        }

        public IssueGroups(IEnumerable<IssueGroup> groups) : base(groups)
        {
        }

        public int Tally(MessageType severity) => this.Count(group => group.Issue.Severity == severity);

        public IssueGroups Filter(Predicate<IssueGroup> keep) => new(this.Where(group => keep(group)));

        public bool HasFix => this.Any(group => group.Issue.Fix != null);

        /// <summary>Runs every group's fix as one undo step; a base's fix also mends its variants. Returns how many ran.</summary>
        public int FixAll()
        {
            var fixes = this.Select(group => group.Issue.Fix).Where(fix => fix != null).ToList();
            if (fixes.Count == 0)
            {
                return 0;
            }

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Fix all validation issues");
            fixes.ForEach(fix => fix());
            Undo.CollapseUndoOperations(undoGroup);
            return fixes.Count;
        }
    }
}
