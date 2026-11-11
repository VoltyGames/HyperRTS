using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Simulation.Combat.Deploy;
using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Combat.Weapons;

namespace HyperRTS.Editor.Validation.Rules.Units
{
    /// <summary>
    /// A mount sharing the unit's target hits only what the primary hits, so orders and the AI, which read the
    /// primary, stay correct. Arcs need a projectile to fly. Extra muzzles follow a first one, and deployed-only weapons need a Deployable.
    /// </summary>
    public sealed class WeaponRule : AuthoringRule<WeaponAuthoring>
    {
        protected override void Check(WeaponAuthoring weapon, ValidationIssues issues)
        {
            CheckFlight(weapon, "The weapon", weapon.stats, issues);
            for (var i = 0; i < weapon.extraWeapons.Count; i++)
            {
                CheckMount(weapon, weapon.extraWeapons[i], $"Extra weapon {i + 1}", issues);
            }
        }

        private static void CheckMount(WeaponAuthoring weapon, WeaponAuthoring.WeaponSlot slot, string label,
            ValidationIssues issues)
        {
            if (slot == null)
            {
                issues.Error(weapon, $"{label} is empty.");
                return;
            }

            CheckFlight(weapon, label, slot.stats, issues);
            if (slot.stats.range <= 0f)
            {
                issues.Error(weapon, $"{label} has no range.");
            }

            if (!slot.independent && !slot.stats.ShootsProjectiles && !Covers(weapon.stats.targets, slot.stats.targets))
            {
                issues.Error(weapon, $"{label} hits {slot.stats.targets} but the primary only " +
                    $"{weapon.stats.targets}: make it independent.");
            }
        }

        private static void CheckFlight(WeaponAuthoring weapon, string label, WeaponStats stats, ValidationIssues issues)
        {
            if (stats.targets == WeaponTargets.None)
            {
                issues.Error(weapon, $"{label} has no targets, so it can never fire.");
            }

            if (stats.minRange >= stats.range)
            {
                issues.Error(weapon, $"{label} has a minimum range {stats.minRange} not below its range {stats.range}.");
            }

            if (stats.arcHeight < 0f)
            {
                issues.Error(weapon, $"{label} has a negative arc height.");
            }

            if (stats.trajectory != Trajectory.Direct && stats.projectilePrefab == null)
            {
                issues.Error(weapon, $"{label} flies {stats.trajectory} but has no projectile prefab.");
            }

            if (stats.extraMuzzles.Count > 0 && stats.muzzle == null)
            {
                issues.Error(weapon, $"{label} has extra muzzles but no first muzzle.");
            }

            if (stats.ShootsProjectiles)
            {
                CheckInterception(weapon, label, stats, issues);
            }

            if (stats.needsDeploy && !weapon.TryGetComponent<DeployableAuthoring>(out _))
            {
                issues.Error(weapon, $"{label} needs deployment but the unit has no Deployable.");
            }
        }

        /// <summary>A weapon that shoots projectiles shoots only them, instantly, at kinds it names.</summary>
        private static void CheckInterception(WeaponAuthoring weapon, string label, WeaponStats stats,
            ValidationIssues issues)
        {
            if (stats.targets != WeaponTargets.Projectile)
            {
                issues.Error(weapon, $"{label} hits projectiles and {stats.targets & ~WeaponTargets.Projectile}: " +
                    "an interceptor targets only Projectile.");
            }

            if (stats.projectilePrefab != null)
            {
                issues.Error(weapon, $"{label} hits projectiles but launches one itself: an interceptor hits instantly.");
            }

            if (stats.interceptKinds == ProjectileKind.None)
            {
                issues.Error(weapon, $"{label} hits projectiles but engages no kind of them.");
            }
        }

        private static bool Covers(WeaponTargets primary, WeaponTargets mount) =>
            primary.HasFlag(mount);
    }
}
