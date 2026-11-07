using HyperRTS.Simulation.Selection;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Base for rules about <typeparamref name="T"/> and its subclasses.</summary>
    public abstract class AuthoringRule<T> : IAuthoringRule where T : Component
    {
        public virtual bool AppliesTo(Component component) => component is T;

        void IAuthoringRule.Check(Component component, ValidationIssues issues) => Check((T)component, issues);

        protected abstract void Check(T component, ValidationIssues issues);

        // The pick box must sit on the entity itself: a child's box would bake onto another entity.
        protected static void FitPickBoxIfMissing(Component component, string consequence, ValidationIssues issues)
        {
            if (!component.TryGetComponent<PickBoxAuthoring>(out _))
            {
                issues.Warn(component, $"No pick box: {consequence}", "Fit Pick Box", () => QuickFixes.FitPickBox(component));
            }
        }

        /// <summary>Warns on <paramref name="owner"/> unless <paramref name="target"/> is a ghost; the fix makes it one.</summary>
        protected static void WarnIfNotGhost(Component owner, GameObject target, string message, ValidationIssues issues,
            string path = null)
        {
            if (!IsGhost(target))
            {
                issues.Warn(owner, message, "Make Ghost", () => Undo.AddComponent<GhostAuthoringComponent>(target), path);
            }
        }

        protected static bool IsGhost(Component component) => IsGhost(component.gameObject);

        protected static bool IsGhost(GameObject go) => go.TryGetComponent<GhostAuthoringComponent>(out _);
    }
}
