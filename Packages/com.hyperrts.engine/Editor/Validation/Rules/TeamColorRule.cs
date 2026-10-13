using System.Linq;
using HyperRTS.Simulation.GameEntities;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Owner colour reaches a unit or building only through a material with <c>_TeamColor</c> (HyperRTS/LitTeam).</summary>
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
            }
        }
    }
}
