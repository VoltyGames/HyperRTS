using System.Linq;
using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Editor.Validation.Sets;

namespace HyperRTS.Editor.Validation.Rules.Units
{
    /// <summary>The project needs exactly one <see cref="TeamColorSettings"/> asset, or <see cref="TeamColorRule"/> has no marker bar.</summary>
    public sealed class TeamColorSettingsRule : IAssetRule
    {
        public void Check(AssetSet assets, ValidationIssues issues)
        {
            var found = assets.All<TeamColorSettings>();
            if (found.Count == 1)
            {
                return;
            }

            if (found.Count == 0)
            {
                issues.Error(null, $"No {nameof(TeamColorSettings)} asset: create one (Create > HyperRTS > Team Colour Settings) for the team marker count.");
                return;
            }

            issues.Error(found[0], $"{found.Count} {nameof(TeamColorSettings)} assets exist; keep one: " +
                string.Join(", ", found.Select(settings => settings.name)));
        }
    }
}
