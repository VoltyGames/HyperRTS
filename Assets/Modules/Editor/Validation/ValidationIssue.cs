using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Validation
{
    /// <summary>One setup problem and the object to fix.</summary>
    public readonly struct ValidationIssue
    {
        public readonly MessageType Severity;
        public readonly string Message;
        public readonly Object Context;

        public ValidationIssue(MessageType severity, string message, Object context)
        {
            Severity = severity;
            Message = message;
            Context = context;
        }

        public override string ToString() => $"HyperRTS: {Message} ({(Context != null ? Context.name : "project")})";
    }
}
