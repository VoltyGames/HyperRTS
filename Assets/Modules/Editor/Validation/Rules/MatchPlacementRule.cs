using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Every scene entity has a player slot and sits inside the map.</summary>
    public sealed class MatchPlacementRule : ISceneRule
    {
        public void Check(SceneSet scenes, ValidationIssues issues)
        {
            // With zero or several matches there is no map to check against; MatchCountRule reports that.
            if (scenes.Matches.Count != 1)
            {
                return;
            }

            var match = scenes.Matches[0];
            var bounds = match.MapRect;
            foreach (var entity in scenes.Entities)
            {
                if (entity.owner > match.players.Count)
                {
                    issues.Warn(entity, $"Owner {entity.owner} has no player slot (the match has {match.players.Count}).");
                }

                var position = entity.transform.position;
                if (!bounds.Contains(new Vector2(position.x, position.z)))
                {
                    issues.Warn(entity, "Outside the match's map bounds.");
                }
            }
        }
    }
}
