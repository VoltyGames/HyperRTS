using HyperRTS.Simulation.Combat;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Projectiles the server spawns must be ghosts for clients to see them fly.</summary>
    public sealed class ProjectileGhostRule : AuthoringRule<WeaponAuthoring>
    {
        protected override void Check(WeaponAuthoring weapon, ValidationIssues issues)
        {
            var projectile = weapon.projectilePrefab;
            if (projectile == null)
            {
                return;
            }

            WarnIfNotGhost(weapon, projectile,
                $"Projectile '{projectile.name}' is not a ghost: clients won't see it fly.", issues);
        }
    }
}
