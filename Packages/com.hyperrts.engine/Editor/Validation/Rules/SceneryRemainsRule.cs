using HyperRTS.Simulation.Destruction;
using HyperRTS.Simulation.GameEntities;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>
    /// Map scenery isn't a ghost, so its remains must not be ghosts either or they would show twice; scenery that is
    /// a ghost needs ghost remains. <see cref="GameEntityRule"/> checks map buildings.
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
