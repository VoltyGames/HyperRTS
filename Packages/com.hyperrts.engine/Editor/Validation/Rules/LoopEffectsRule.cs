using HyperRTS.Simulation.Effects;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Every loop plays a valid effect rig from a socket.</summary>
    public sealed class LoopEffectsRule : AuthoringRule<LoopEffectsAuthoring>
    {
        protected override void Check(LoopEffectsAuthoring effects, ValidationIssues issues)
        {
            for (var i = 0; i < effects.loops.Length; i++)
            {
                var loop = effects.loops[i];
                EffectRigCheck.Check(effects, loop.rig, $"Loop {i}", issues);
                if (loop.socket == null)
                {
                    issues.Error(effects, $"Loop {i} has no socket.");
                }
            }
        }
    }
}
