using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Validation;
using UnityEditor;

namespace HyperRTS.Editor.Commands
{
    /// <summary>A validation pass as the <c>validate</c> command returns it: counts, then each issue with where to fix it.</summary>
    public sealed class ValidationReport
    {
        public int errors;
        public int warnings;
        public List<Issue> issues;

        public static ValidationReport From(IReadOnlyCollection<ValidationIssue> found) => new()
        {
            errors = found.Count(issue => issue.Severity == MessageType.Error),
            warnings = found.Count(issue => issue.Severity == MessageType.Warning),
            issues = found.OrderByDescending(issue => issue.Severity).Select(Describe).ToList(),
        };

        private static Issue Describe(ValidationIssue issue)
        {
            var asset = issue.Context == null ? null : AssetDatabase.GetAssetPath(issue.Context);
            return new Issue
            {
                severity = issue.Severity.ToString(),
                message = issue.Message,
                asset = string.IsNullOrEmpty(asset) ? null : asset,
                name = issue.Context == null ? null : issue.Context.name,
                fix = issue.FixLabel,
            };
        }

        /// <summary>One issue; <c>asset</c> is null for a scene object, which <c>name</c> still names.</summary>
        public sealed class Issue
        {
            public string severity;
            public string message;
            public string asset;
            public string name;
            public string fix;
        }
    }
}
