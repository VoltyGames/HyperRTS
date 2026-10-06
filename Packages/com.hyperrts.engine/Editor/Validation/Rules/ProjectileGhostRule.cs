using HyperRTS.Simulation.Combat;
using Unity.NetCode;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Projectiles the server spawns must be ghosts for clients to see them fly.</summary>
    public sealed class ProjectileGhostRule : AuthoringRule<WeaponAuthoring>
    {
        protected override void Check(WeaponAuthoring weapon, ValidationIssues issues)
        {
            var projectile = weapon.projectilePrefab;
            if (projectile == null || projectile.TryGetComponent<GhostAuthoringComponent>(out _))
            {
                return;
            }

            issues.Warn(weapon, $"Projectile '{projectile.name}' is not a ghost: clients won't see it fly.",
                "Make Ghost", () => QuickFixes.AddComponent(projectile, typeof(GhostAuthoringComponent)));
        }
    }
}
