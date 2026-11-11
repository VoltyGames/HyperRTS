using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Simulation.Death;


namespace HyperRTS.Editor.Validation.Rules.Effects
{
    /// <summary>The spawn effect is a valid effect rig.</summary>
    public sealed class SpawnEffectRule : AuthoringRule<SpawnEffectAuthoring>
    {
        protected override void Check(SpawnEffectAuthoring spawn, ValidationIssues issues) =>
            EffectRigCheck.Check(spawn, spawn.effect, "Spawn effect", issues);
    }
}
