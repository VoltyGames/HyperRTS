using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using Unity.NetCode;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Warns when a <see cref="RequiresGhostAttribute"/> component is not on a ghost.</summary>
    public sealed class RequiresGhostRule : IAuthoringRule
    {
        // Runs on every component each inspector Layout, so reflect once per type.
        private static readonly Dictionary<Type, bool> Cache = new();

        public bool AppliesTo(Component component)
        {
            var type = component.GetType();
            if (!Cache.TryGetValue(type, out var required))
            {
                Cache[type] = required = type.IsDefined(typeof(RequiresGhostAttribute), true);
            }

            return required;
        }

        public void Check(Component component, ValidationIssues issues) =>
            WarnIfNotGhost(component, component.gameObject, "Not a ghost: multiplayer clients won't receive it.",
                issues);

        /// <summary>
        /// Warns on <paramref name="owner"/> unless <paramref name="target"/> is a ghost; the fix makes it one.
        /// </summary>
        public static void WarnIfNotGhost(Component owner, GameObject target, string message, ValidationIssues issues)
        {
            if (!target.TryGetComponent<GhostAuthoringComponent>(out _))
            {
                issues.Warn(owner, message, "Make Ghost", () => Undo.AddComponent<GhostAuthoringComponent>(target));
            }
        }
    }
}
