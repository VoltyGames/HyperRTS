using HyperRTS.Simulation.Effects;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Motion dust needs valid surface rigs and a socket to kick them up from.</summary>
    public sealed class MotionDustRule : AuthoringRule<MotionDustAuthoring>
    {
        protected override void Check(MotionDustAuthoring dust, ValidationIssues issues)
        {
            EffectRigCheck.CheckSurfaceEffects(dust, dust.effects, "Motion dust", issues);
            if (dust.socket == null)
            {
                issues.Error(dust, "Motion dust has no socket.");
            }
        }
    }
}
