using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.GameEntities;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>
    /// Map scenery isn't a ghost: every client spawns its remains itself from the scenery's state, so remains that are
    /// ghosts would show twice. Scenery that is a ghost (one the server spawns) needs ghost remains instead.
    /// </summary>
    public sealed class SceneryRemainsRule : AuthoringRule<DestructibleAuthoring>
    {
        protected override void Check(DestructibleAuthoring scenery, ValidationIssues issues)
        {
            var remains = scenery.remains;
            if (remains == null || scenery.TryGetComponent<GameEntityAuthoring>(out _))
            {
                return;
            }

            if (IsGhost(scenery))
            {
                WarnIfNotGhost(scenery, remains,
                    $"Remains '{remains.name}' are not a ghost, so clients won't see what this ghost leaves.", issues);
                return;
            }

            if (IsGhost(remains))
            {
                issues.Warn(scenery, $"Remains '{remains.name}' are a ghost: each client spawns map scenery's " +
                                     "remains itself, so they would show twice.");
            }
        }
    }
}
