using HyperRTS.Simulation.Combat;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>The spawn effect is a valid effect rig.</summary>
    public sealed class SpawnEffectRule : AuthoringRule<SpawnEffectAuthoring>
    {
        protected override void Check(SpawnEffectAuthoring spawn, ValidationIssues issues) =>
            EffectRigCheck.Check(spawn, spawn.effect, "Spawn effect", issues);
    }
}
