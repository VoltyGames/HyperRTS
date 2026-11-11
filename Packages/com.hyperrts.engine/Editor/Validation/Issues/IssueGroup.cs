using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Authoring;
using UnityEditor;

namespace HyperRTS.Editor.Validation.Issues
{
    /// <summary>One issue on a base prefab with the same issue on the variants that inherit it.</summary>
    public readonly struct IssueGroup
    {
        public readonly ValidationIssue Issue;
        public readonly IReadOnlyList<ValidationIssue> Variants;

        public IssueGroup(ValidationIssue issue, IReadOnlyList<ValidationIssue> variants = null)
        {
            Issue = issue;
            Variants = variants ?? new List<ValidationIssue>();
        }

        /// <summary>"and 2 variants"; empty when there are none.</summary>
        public string VariantText => Variants.Count == 0 ? "" : "and " + IssueBadge.CountText(Variants.Count, "variant");

        /// <summary>Whether the message, the asset's name or path or the rule contains <paramref name="search"/>, ignoring case.</summary>
        public bool Matches(string search)
        {
            if (string.IsNullOrEmpty(search))
            {
                return true;
            }

            var asset = Issue.Context == null ? "" : Issue.Context.name + " " + AssetDatabase.GetAssetPath(Issue.Context);
            return new[] { Issue.Message, asset, Issue.Rule }.Any(text => text != null && text.Contains(search, System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
