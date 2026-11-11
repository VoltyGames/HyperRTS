using System;
using UnityEditor;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Validation.Issues
{
    /// <summary>One setup problem, the object to fix and, when there is an obvious one, a one-click fix.</summary>
    public struct ValidationIssue
    {
        public MessageType Severity;
        public string Message;
        public Object Context;
        public string FixLabel;
        public Action Fix;

        /// <summary>The rule that found it, filled in by the validator.</summary>
        public string Rule;

        /// <summary>Property path of the field at fault, such as "stats.range"; empty when no one field is.</summary>
        public string Path;

        public ValidationIssue FoundBy(string rule)
        {
            var found = this;
            found.Rule = rule;
            return found;
        }

        public override string ToString() => $"HyperRTS: {Message} ({(Context != null ? Context.name : "project")})";
    }
}
