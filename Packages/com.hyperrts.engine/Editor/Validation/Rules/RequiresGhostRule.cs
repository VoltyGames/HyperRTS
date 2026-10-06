using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Whatever the server spawns or changes must be a ghost to reach multiplayer clients.</summary>
    public abstract class RequiresGhostRule<T> : AuthoringRule<T> where T : Component
    {
        protected override void Check(T component, ValidationIssues issues) =>
            WarnIfNotGhost(component, component.gameObject, "Not a ghost: multiplayer clients won't receive it.", issues);
    }
}
