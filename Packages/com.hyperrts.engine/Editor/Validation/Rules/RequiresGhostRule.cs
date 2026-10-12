using HyperRTS.Simulation.Common;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Warns when a <see cref="RequiresGhostAttribute"/> component is not on a ghost.</summary>
    public sealed class RequiresGhostRule : AuthoringRule<Component>
    {
        public override bool AppliesTo(Component component) =>
            AttributeCache<RequiresGhostAttribute>.Of(component.GetType()).Length > 0;

        protected override void Check(Component component, ValidationIssues issues) =>
            WarnIfNotGhost(component, component.gameObject, "Not a ghost: multiplayer clients won't receive it.", issues);
    }
}
