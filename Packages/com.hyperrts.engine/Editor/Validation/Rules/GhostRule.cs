using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Resources;
using Unity.NetCode;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>
    /// Everything the server spawns or changes must be a ghost to reach clients: units, buildings, resource nodes,
    /// projectiles and the match. Clients also need physics running to click on them.
    /// </summary>
    public sealed class GhostRule : IAuthoringRule
    {
        public bool AppliesTo(Component component) =>
            component is GameEntityAuthoring or ResourceNodeAuthoring or MatchAuthoring or WeaponAuthoring;

        public void Check(Component component, ValidationIssues issues)
        {
            if (component is WeaponAuthoring weapon)
            {
                CheckProjectile(weapon, issues);
                return;
            }

            if (!component.TryGetComponent<GhostAuthoringComponent>(out _))
            {
                issues.Warn(component, "Not a ghost: multiplayer clients won't receive it.", "Make Ghost",
                    () => Undo.AddComponent<GhostAuthoringComponent>(component.gameObject));
            }

            if (component is MatchAuthoring match)
            {
                CheckClientPhysics(match, issues);
            }
        }

        private static void CheckProjectile(WeaponAuthoring weapon, ValidationIssues issues)
        {
            var projectile = weapon.projectilePrefab;
            if (projectile != null && !projectile.TryGetComponent<GhostAuthoringComponent>(out _))
            {
                issues.Warn(weapon, $"Projectile '{projectile.name}' is not a ghost: clients won't see it fly.",
                    "Make Ghost", () => Undo.AddComponent<GhostAuthoringComponent>(projectile));
            }
        }

        private static void CheckClientPhysics(MatchAuthoring match, ValidationIssues issues)
        {
            var hasConfig = match.TryGetComponent<NetCodePhysicsConfig>(out var config);
            if (hasConfig && config.PhysicGroupRunMode == PhysicGroupRunMode.AlwaysRun)
            {
                return;
            }

            issues.Warn(match, "Clients can't click units: Netcode only runs physics for predicted ghosts.",
                "Always Run Physics", () =>
                {
                    var target = hasConfig ? config : Undo.AddComponent<NetCodePhysicsConfig>(match.gameObject);
                    QuickFixes.Edit(target, "Always Run Physics",
                        () => target.PhysicGroupRunMode = PhysicGroupRunMode.AlwaysRun);
                });
        }
    }
}
