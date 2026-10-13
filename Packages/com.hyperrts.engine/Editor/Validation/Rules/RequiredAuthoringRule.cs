using System;
using HyperRTS.Simulation.Common;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Reports a missing <see cref="RequiresAuthoringAttribute"/> dependency, with an Add fix for a concrete type.</summary>
    public sealed class RequiredAuthoringRule : AuthoringRule<Component>
    {
        public override bool AppliesTo(Component component) => Required(component).Length > 0;

        protected override void Check(Component component, ValidationIssues issues)
        {
            foreach (var required in Required(component))
            {
                if (component.TryGetComponent(required.Type, out _))
                {
                    continue;
                }

                var message = $"{DisplayName(component.GetType())} needs {required.Description} on the same object.";
                if (required.Type.IsAbstract)
                {
                    issues.Warn(component, message);
                    continue;
                }

                issues.Warn(component, message, "Add " + DisplayName(required.Type).Replace(" Authoring", ""),
                    () => Undo.AddComponent(component.gameObject, required.Type));
            }
        }

        private static RequiresAuthoringAttribute[] Required(Component component) =>
            AttributeCache<RequiresAuthoringAttribute>.Get(component.GetType());

        private static string DisplayName(Type type) => ObjectNames.NicifyVariableName(type.Name);
    }
}
