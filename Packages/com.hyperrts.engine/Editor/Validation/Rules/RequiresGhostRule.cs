using Unity.NetCode;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Whatever the server spawns or changes must be a ghost to reach multiplayer clients.</summary>
    public abstract class RequiresGhostRule<T> : AuthoringRule<T> where T : Component
    {
        protected override void Check(T component, ValidationIssues issues)
        {
            if (!component.TryGetComponent<GhostAuthoringComponent>(out _))
            {
                issues.Warn(component, "Not a ghost: multiplayer clients won't receive it.", "Make Ghost",
                    () => QuickFixes.AddComponent(component.gameObject, typeof(GhostAuthoringComponent)));
            }
        }
    }
}
