using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HyperRTS.Simulation.Common;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Reports a missing <see cref="RequiresAuthoringAttribute"/> dependency on any component.</summary>
    public sealed class RequiredAuthoringRule : IAuthoringRule
    {
        // Runs on every component each inspector Layout, so reflect once per type.
        private static readonly Dictionary<Type, RequiresAuthoringAttribute[]> Cache = new();

        public bool AppliesTo(Component component) => Required(component.GetType()).Length > 0;

        public void Check(Component component, ValidationIssues issues)
        {
            foreach (var required in Required(component.GetType()))
            {
                if (!component.TryGetComponent(required.Type, out _))
                {
                    var name = ObjectNames.NicifyVariableName(component.GetType().Name);
                    issues.Warn(component, $"{name} needs {required.Description} on the same object.");
                }
            }
        }

        private static RequiresAuthoringAttribute[] Required(Type type)
        {
            if (!Cache.TryGetValue(type, out var required))
            {
                Cache[type] = required = type.GetCustomAttributes<RequiresAuthoringAttribute>(true).ToArray();
            }

            return required;
        }
    }
}
