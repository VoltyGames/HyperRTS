using System.Collections.Generic;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Validation.Rules;
using UnityEngine;

namespace HyperRTS.Editor.Validation
{
    /// <summary>Runs every <see cref="IAuthoringRule"/> that applies to one component; shared by inspectors and the validator.</summary>
    public static class AuthoringChecks
    {
        private static readonly IAuthoringRule[] Rules = TypeDiscovery.Instances<IAuthoringRule>();

        public static List<ValidationIssue> For(Component component)
        {
            var issues = new ValidationIssues();
            foreach (var rule in Rules)
            {
                if (rule.AppliesTo(component))
                {
                    rule.Check(component, issues);
                }
            }

            return issues;
        }
    }
}
