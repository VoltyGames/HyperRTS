using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

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
            CheckEmptyEntries(owner, options, option => option == null,
                $"{label} list has empty entries that will be ignored.", issues);

            foreach (var option in options)
            {
                CheckPrefabReference(owner, option, label, issues, () => QuickFixes.UsePrefab(owner, options, option));
            }
        }

        /// <summary>
        /// A referenced prefab must be an asset: a scene object stops existing once the scene bakes. Offers
        /// <paramref name="usePrefab"/> as a fix when the object is an instance of a prefab.
        /// </summary>
        protected static void CheckPrefabReference(Component owner, Object reference, string label,
            ValidationIssues issues, Action usePrefab = null)
        {
            if (reference == null || PrefabUtility.IsPartOfPrefabAsset(reference))
            {
                return;
            }

            var message = $"{label} '{reference.name}' is a scene object; reference the prefab asset.";
            var hasSource = PrefabUtility.GetCorrespondingObjectFromSource(reference) != null;
            if (usePrefab != null && hasSource)
            {
                issues.Warn(owner, message, "Use Prefab", usePrefab);
                return;
            }

            issues.Warn(owner, message);
        }

        /// <summary>Warns about list entries the baker skips, with a fix that removes them.</summary>
        protected static void CheckEmptyEntries<TItem>(Component owner, List<TItem> list, Predicate<TItem> isEmpty,
            string message, ValidationIssues issues)
        {
            if (list.Exists(isEmpty))
            {
                issues.Warn(owner, message, "Remove Empty", () => QuickFixes.RemoveEmpty(owner, list, isEmpty));
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
