using System;
using UnityEditor;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Validation
{
    /// <summary>One setup problem, the object to fix and, when there is an obvious one, a one-click fix.</summary>
    public readonly struct ValidationIssue
    {
        public readonly MessageType Severity;
        public readonly string Message;
        public readonly Object Context;
        public readonly string FixLabel;
        public readonly Action Fix;

        /// <summary>The rule that found it, filled in by the validator.</summary>
        public readonly string Rule;

        public ValidationIssue(MessageType severity, string message, Object context, string fixLabel = null, Action fix = null, string rule = null)
        {
            Severity = severity;
            Message = message;
            Context = context;
            FixLabel = fixLabel;
            Fix = fix;
            Rule = rule;
        }

        public ValidationIssue FoundBy(string rule) => new(Severity, Message, Context, FixLabel, Fix, rule);

        public override string ToString() => $"HyperRTS: {Message} ({(Context != null ? Context.name : "project")})";
    }
}
