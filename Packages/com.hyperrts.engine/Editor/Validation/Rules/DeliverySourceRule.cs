using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Resources;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>A delivery source names an unarmed carrier prefab and the resource it pays.</summary>
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
            CheckUnarmed(source, issues);
        }

        /// <summary>Combat owns an armed unit's movement, so an armed carrier would stop to fight and never deliver.</summary>
        private static void CheckUnarmed(DeliverySourceAuthoring source, ValidationIssues issues)
        {
            if (source.carrier == null)
            {
                return;
            }

            var weapon = source.carrier.GetComponentInChildren<WeaponAuthoring>(true);
            if (weapon != null)
            {
                issues.Error(source, $"Carrier '{source.carrier.name}' has a weapon on '{weapon.name}'; carriers must be unarmed.");
            }
        }
    }
}
