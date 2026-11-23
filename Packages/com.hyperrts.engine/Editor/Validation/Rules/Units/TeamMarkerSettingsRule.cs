using System.Linq;
using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Editor.Validation.Sets;

namespace HyperRTS.Editor.Validation.Rules.Units
{
    /// <summary>The project needs exactly one <see cref="TeamMarkerSettings"/> asset, or <see cref="TeamColorRule"/> has no marker bar.</summary>
    public sealed class TeamMarkerSettingsRule : IAssetRule
    {
        public void Check(AssetSet assets, ValidationIssues issues)
        {
            var found = assets.All<TeamMarkerSettings>();
            if (found.Count == 1)
            {
                return;
            }

            if (found.Count == 0)
            {
                issues.Error(null, $"No {nameof(TeamMarkerSettings)} asset: create one (Create > HyperRTS > Team Marker Settings) for the team marker count.");
                return;
            }

            issues.Error(found[0], $"{found.Count} {nameof(TeamMarkerSettings)} assets exist; keep one: " +
                string.Join(", ", found.Select(settings => settings.name)));
        }
    }
}
