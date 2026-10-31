using System;
using System.Collections.Generic;
using UnityEditor;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Validation
{
    /// <summary>Issues collected by a validation pass, with one shorthand per severity.</summary>
    public sealed class ValidationIssues : List<ValidationIssue>
    {
        public void Error(Object context, string message) =>
            Add(new ValidationIssue(MessageType.Error, message, context));

        public void Warn(Object context, string message, string fixLabel = null, Action fix = null) =>
            Add(new ValidationIssue(MessageType.Warning, message, context, fixLabel, fix));

        /// <summary>Names the rule behind every issue added since <paramref name="start"/>.</summary>
        public void StampFrom(int start, string rule)
        {
            for (var i = start; i < Count; i++)
            {
                this[i] = this[i].FoundBy(rule);
            }
        }

        public void Info(Object context, string message) =>
            Add(new ValidationIssue(MessageType.Info, message, context));
    }
}
