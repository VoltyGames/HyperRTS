using System.Linq;
using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Simulation.GameEntities;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules.Units
{
    /// <summary>
    /// Owner colour reaches a unit or building only through a material with <c>_TeamColor</c> (HyperRTS/LitTeam), and a
    /// vehicle's mask must cover enough of its top view to read from the match camera.
    /// </summary>
    public sealed class TeamColorRule : AuthoringRule<GameEntityAuthoring>
    {
        private const string TeamColor = "_TeamColor";

        protected override void Check(GameEntityAuthoring entity, ValidationIssues issues)
        {
            var materials = entity.GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null)
                .ToList();
            if (materials.Count == 0)
            {
                return;
            }

            if (!materials.Any(material => material.HasProperty(TeamColor)))
            {
                issues.Warn(entity, "No material has _TeamColor (use HyperRTS/LitTeam): it won't show its owner's colour.");
                return;
            }

            if (!TopDownTeamColor.IsMobileVehicle(entity) || !TeamColorSettings.TryGetMinTopDownShare(out var minimum))
            {
                return;
            }

            if (new TopDownTeamColor().TryGetShare(entity.gameObject, out var share) && share < minimum)
            {
                issues.Warn(entity, $"Only {share:P0} of the unit seen from above shows team colour, below {minimum:P0}: " +
                    "widen the _TeamMask so its owner reads from the match camera.");
            }
        }
    }
}
