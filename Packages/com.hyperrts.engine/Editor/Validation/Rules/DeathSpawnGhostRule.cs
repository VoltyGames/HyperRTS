using HyperRTS.Simulation.GameEntities;
using Unity.NetCode;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>The server spawns a ghost's death spawn (wreck, ruins), so it must be a ghost for clients to see it.</summary>
    public sealed class DeathSpawnGhostRule : AuthoringRule<GameEntityAuthoring>
    {
        protected override void Check(GameEntityAuthoring entity, ValidationIssues issues)
        {
            var spawn = entity.deathSpawn;
            if (spawn == null || !entity.TryGetComponent<GhostAuthoringComponent>(out _))
            {
                return;
            }

            RequiresGhostRule.WarnIfNotGhost(entity, spawn, $"Death spawn '{spawn.name}' is not a ghost: clients won't see it.",
                issues);
        }
    }
}
