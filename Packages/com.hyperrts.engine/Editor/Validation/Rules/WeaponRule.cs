using HyperRTS.Simulation.Combat;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>The projectile is a prefab asset.</summary>
    public sealed class WeaponRule : AuthoringRule<WeaponAuthoring>
    {
        protected override void Check(WeaponAuthoring weapon, ValidationIssues issues) =>
            CheckPrefabReference(weapon, weapon.projectilePrefab, "Projectile prefab", issues);
    }
}
