using HyperRTS.Simulation.Combat;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>
    /// Projectiles are prefab ghosts, since the server spawns them and clients must see them fly. A mount sharing the
    /// unit's target hits only what the primary hits, so orders and the AI, which read the primary, stay correct. Arcs
    /// need a projectile to fly.
    /// </summary>
    public sealed class WeaponRule : AuthoringRule<WeaponAuthoring>
    {
        protected override void Check(WeaponAuthoring weapon, ValidationIssues issues)
        {
            CheckProjectile(weapon, weapon.projectilePrefab, "Projectile", issues);
            CheckFlight(weapon, "The weapon", weapon.trajectory, weapon.arcHeight, weapon.minRange, weapon.range,
                weapon.projectilePrefab, issues);
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

            CheckProjectile(weapon, slot.projectilePrefab, $"{label} projectile", issues);
            CheckFlight(weapon, label, slot.trajectory, slot.arcHeight, slot.minRange, slot.range, slot.projectilePrefab,
                issues);
            if (slot.range <= 0f)
            {
                issues.Error(weapon, $"{label} has no range.");
            }

            if (!slot.independent && !Covers(weapon.targets, slot.targets))
            {
                issues.Error(weapon,
                    $"{label} hits {slot.targets} but the primary only {weapon.targets}: make it independent.");
            }
        }

        private static void CheckProjectile(WeaponAuthoring weapon, GameObject projectile, string label,
            ValidationIssues issues)
        {
            if (projectile == null)
            {
                return;
            }

            CheckPrefabReference(weapon, projectile, $"{label} prefab", issues);
            WarnIfNotGhost(weapon, projectile, $"{label} '{projectile.name}' is not a ghost: clients won't see it fly.",
                issues);
        }

        private static void CheckFlight(WeaponAuthoring weapon, string label, Trajectory trajectory, float arcHeight,
            float minRange, float range, GameObject projectile, ValidationIssues issues)
        {
            if (minRange >= range)
            {
                issues.Error(weapon, $"{label} has a minimum range {minRange} not below its range {range}.");
            }

            if (arcHeight < 0f)
            {
                issues.Error(weapon, $"{label} has a negative arc height.");
            }

            if (trajectory != Trajectory.Direct && projectile == null)
            {
                issues.Error(weapon, $"{label} flies {trajectory} but has no projectile prefab.");
            }
        }

        private static bool Covers(WeaponTargets primary, WeaponTargets mount) =>
            primary == mount || primary == WeaponTargets.SurfaceAndAir;
    }
}
