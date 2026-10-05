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

        public void Info(Object context, string message) =>
            Add(new ValidationIssue(MessageType.Info, message, context));
    }
}
