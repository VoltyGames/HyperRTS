using HyperRTS.Simulation.Combat;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>The projectile is a prefab asset and a ghost, since the server spawns it and clients must see it fly.</summary>
    public sealed class WeaponRule : AuthoringRule<WeaponAuthoring>
    {
        protected override void Check(WeaponAuthoring weapon, ValidationIssues issues)
        {
            var projectile = weapon.projectilePrefab;
            if (projectile == null)
            {
                return;
            }

            CheckPrefabReference(weapon, projectile, "Projectile prefab", issues);
            WarnIfNotGhost(weapon, projectile, $"Projectile '{projectile.name}' is not a ghost: clients won't see it fly.",
                issues);
        }
    }
}
