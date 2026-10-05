using HyperRTS.Simulation.Resources;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>A node has a resource type and can be right-clicked.</summary>
    public sealed class ResourceNodeRule : AuthoringRule<ResourceNodeAuthoring>
    {
        protected override void Check(ResourceNodeAuthoring node, ValidationIssues issues)
        {
            if (node.type == null)
            {
                issues.Error(node, "Resource node has no resource type.");
            }

            FitColliderIfMissing(node, "harvesters can't be right-click ordered to gather here.", issues);
        }
    }
}
