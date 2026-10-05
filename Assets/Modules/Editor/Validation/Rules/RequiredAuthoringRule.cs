using System.Reflection;
using HyperRTS.Simulation.Common;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Reports a missing <see cref="RequiresAuthoringAttribute"/> dependency on any component.</summary>
    public sealed class RequiredAuthoringRule : IAuthoringRule
    {
        public bool AppliesTo(Component component) => component.GetType().IsDefined(typeof(RequiresAuthoringAttribute), true);

        public void Check(Component component, ValidationIssues issues)
        {
            foreach (var required in component.GetType().GetCustomAttributes<RequiresAuthoringAttribute>(true))
            {
                if (!component.TryGetComponent(required.Type, out _))
                {
                    var name = ObjectNames.NicifyVariableName(component.GetType().Name);
                    issues.Warn(component, $"{name} needs {required.Description} on the same object.");
                }
            }
        }
    }
}
