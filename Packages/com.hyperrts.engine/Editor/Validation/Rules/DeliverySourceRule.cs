using HyperRTS.Simulation.Resources;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>A delivery source names its carrier prefab and the resource it pays.</summary>
    public sealed class DeliverySourceRule : AuthoringRule<DeliverySourceAuthoring>
    {
        protected override void Check(DeliverySourceAuthoring source, ValidationIssues issues)
        {
            if (source.carrier == null)
            {
                issues.Error(source, "Delivery source has no carrier prefab.");
            }

            if (source.type == null)
            {
                issues.Error(source, "Delivery source has no resource type.");
            }

            CheckPrefabReference(source, source.carrier, "Carrier", issues);
        }
    }
}
