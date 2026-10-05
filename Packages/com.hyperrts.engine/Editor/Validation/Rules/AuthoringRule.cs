using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Base for rules about <typeparamref name="T"/> and its subclasses.</summary>
    public abstract class AuthoringRule<T> : IAuthoringRule where T : Component
    {
        public bool AppliesTo(Component component) => component is T;

        void IAuthoringRule.Check(Component component, ValidationIssues issues) => Check((T)component, issues);

        protected abstract void Check(T component, ValidationIssues issues);

        /// <summary>Option lists must hold prefab assets: a scene object stops existing once the scene bakes.</summary>
        protected static void CheckPrefabOptions<TOption>(Component owner, List<TOption> options, string label,
            ValidationIssues issues) where TOption : Object
        {
            if (options.Exists(option => option == null))
            {
                issues.Warn(owner, $"{label} list has empty entries that will be ignored.", "Remove Empty",
                    () => QuickFixes.RemoveEmpty(owner, options, option => option == null));
            }

            foreach (var option in options)
            {
                if (option == null || PrefabUtility.IsPartOfPrefabAsset(option))
                {
                    continue;
                }

                var message = $"{label} '{option.name}' is a scene object; reference the prefab asset.";
                if (PrefabUtility.GetCorrespondingObjectFromSource(option) != null)
                {
                    issues.Warn(owner, message, "Use Prefab", () => QuickFixes.UsePrefab(owner, options, option));
                }
                else
                {
                    issues.Warn(owner, message);
                }
            }
        }

        protected static void FitColliderIfMissing(Component component, string consequence, ValidationIssues issues)
        {
            if (component.GetComponentInChildren<Collider>() == null)
            {
                issues.Warn(component, $"No collider: {consequence}", "Fit Collider", () => QuickFixes.FitCollider(component));
            }
        }
    }
}
