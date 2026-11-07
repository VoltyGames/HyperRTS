using System;
using System.Collections.Generic;
using UnityEditor;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Validation
{
    /// <summary>Issues collected by a validation pass, with one shorthand per severity.</summary>
    public sealed class ValidationIssues : List<ValidationIssue>
    {
        public void Error(Object context, string message, string fixLabel = null, Action fix = null, string path = null) =>
            Add(new ValidationIssue(MessageType.Error, message, context, fixLabel, fix, path: path));

        public void Warn(Object context, string message, string fixLabel = null, Action fix = null, string path = null) =>
            Add(new ValidationIssue(MessageType.Warning, message, context, fixLabel, fix, path: path));

        /// <summary>
        /// Each issue once, with the same issue (rule and message) on prefab variants folded under the topmost prefab
        /// in the chain that has it.
        /// </summary>
        public IssueGroups GroupVariants()
        {
            var first = new Dictionary<(string, string, Object), int>();
            for (var i = 0; i < Count; i++)
            {
                first.TryAdd(Key(this[i], this[i].Context), i);
            }

            var variants = new Dictionary<int, List<ValidationIssue>>();
            var folded = new HashSet<int>();
            for (var i = 0; i < Count; i++)
            {
                var top = TopmostSource(this[i], first);
                if (top >= 0 && top != i)
                {
                    folded.Add(i);
                    variants.TryAdd(top, new List<ValidationIssue>());
                    variants[top].Add(this[i]);
                }
            }

            var groups = new IssueGroups();
            for (var i = 0; i < Count; i++)
            {
                if (!folded.Contains(i))
                {
                    groups.Add(new IssueGroup(this[i], variants.GetValueOrDefault(i)));
                }
            }

            return groups;
        }

        private static (string, string, Object) Key(ValidationIssue issue, Object context) => (issue.Rule, issue.Message, context);

        // Index of the same issue on the highest prefab asset this one's object derives from, or -1 for none.
        private static int TopmostSource(ValidationIssue issue, Dictionary<(string, string, Object), int> first)
        {
            if (issue.Context == null || !EditorUtility.IsPersistent(issue.Context))
            {
                return -1;
            }

            var top = -1;
            for (var source = PrefabUtility.GetCorrespondingObjectFromSource(issue.Context); source != null;
                 source = PrefabUtility.GetCorrespondingObjectFromSource(source))
            {
                if (first.TryGetValue(Key(issue, source), out var index))
                {
                    top = index;
                }
            }

            return top;
        }

        /// <summary>Names the rule behind every issue added since <paramref name="start"/>.</summary>
        public void StampFrom(int start, string rule)
        {
            for (var i = start; i < Count; i++)
            {
                this[i] = this[i].FoundBy(rule);
            }
        }
    }
}
