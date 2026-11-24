using System.Text.RegularExpressions;
using HyperRTS.Editor.Validation.Issues;

namespace HyperRTS.Editor.Validation.Rules.UI
{
    using Match = System.Text.RegularExpressions.Match;

    /// <summary>Reports the <c>@import</c> and <c>url()</c> links of a style sheet that name no asset.</summary>
    internal sealed class UssLinkCheck
    {
        private static readonly Regex Link = new(
            @"url\(\s*(?:""([^""]*)""|'([^']*)'|([^)\s]*))\s*\)|@import\s+(?:""([^""]*)""|'([^']*)')");

        public void Run(UIFile file, UIProject project, ValidationIssues issues)
        {
            foreach (Match match in Link.Matches(file.Code))
            {
                var link = UIProject.FirstGroup(match);
                var target = link.Length > 0 ? file.Resolve(link) : null;
                if (target != null && !project.AssetExists(target))
                {
                    issues.Error(project.AssetAt(file.Path),
                        $"{file.Path}:{file.LineAt(match.Index)}: the link '{link}' names no asset.");
                }
            }
        }
    }
}
