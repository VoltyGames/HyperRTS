using HyperRTS.Simulation.Combat;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>An interceptor that needs deployment sits on a Deployable unit.</summary>
    public sealed class InterceptorRule : AuthoringRule<InterceptorAuthoring>
    {
        protected override void Check(InterceptorAuthoring interceptor, ValidationIssues issues)
        {
            if (interceptor.needsDeploy && !interceptor.TryGetComponent<DeployableAuthoring>(out _))
            {
                issues.Error(interceptor, "The interceptor needs deployment but the unit has no Deployable.");
            }
        }
    }
}
